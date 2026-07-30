using FlowTiles.PortalPaths;
using NUnit.Framework.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct FollowPathsJob : IJobEntity {

        [ReadOnly] public PathableGraph Graph;
        [ReadOnly] public PathCache PathCache;
        [ReadOnly] public FlowCache FlowCache;
        [ReadOnly] public LineCache LineCache;
        [ReadOnly] public NativeHashSet<int4> PathInFlightKeys;

        public EntityCommandBuffer.ParallelWriter ECB;

        [BurstCompile]
        private void Execute(
                Entity entity,
                RefRO<FlowPosition> position, 
                RefRO<FlowGoal> goal, 
                ref FlowProgress progress,
                ref FlowDirection result, 
                [ChunkIndexInQuery] int sortKey) {

            result.Direction = 0;
            progress.IsAttachedToFlow = false;

            // Check pathfinding is enabled
            if (!goal.ValueRO.HasGoal) {
                progress.IsAttachedToPath = false;
                result.PathIsImpossible = false;
                return;
            }

            // Check start and dest are valid
            var currentCell = position.ValueRO.PositionCell;
            var goalCell = goal.ValueRO.Goal;
            var levelSize = Graph.Bounds.SizeCells;
            var travelType = goal.ValueRO.TravelType;

            if (!Graph.Bounds.ContainsCell(currentCell) || !Graph.Bounds.ContainsCell(goalCell)) {
                progress.IsAttachedToPath = false;
                result.PathIsImpossible = true;
                return;
            }

            // Check path can exist (continents match)
            var startCell = currentCell;
            var startSector = Graph.Layout.CellToSectorIndex(startCell);
            var startMap = Graph.IndexToSectorMap(startSector, travelType);
            var startContinent = startMap.GetRoot(startCell).Continent;
            
            var destCell = goalCell;
            var destSector = Graph.Layout.CellToSectorIndex(destCell);
            var destMap = Graph.IndexToSectorMap(destSector, travelType);
            var destContinent = destMap.GetRoot(destCell).Continent;

            if (startContinent != destContinent) {
                progress.IsAttachedToPath = false;
                result.PathIsImpossible = true;
                return;
            }
            result.PathIsImpossible = false;

            // Apply start and dest cell rounding
            startCell = Graph.TryApplySectorRounding(startCell, travelType);
            destCell = Graph.TryApplySectorRounding(destCell, travelType);

            var startIsland = startMap.GetCellIsland(startCell);
            var destIsland = destMap.GetCellIsland(destCell);

            // Override direction near destination
            var smoothPos = position.ValueRO.Position;
            var pos = position.ValueRO.PositionCell;
            if (currentCell.Equals(goalCell)) {
                result.Direction = 0;
                return;
            } 
            if (startSector == destSector && startCell.Equals(destCell)) {
                var directDir = math.normalizesafe(goalCell - currentCell);
                result.Direction = directDir;
                return;
            }

            // Attach to a path
            if (!progress.IsAttachedToPath) {

                // Generate or retrieve a path
                var pathKey = CacheKeys.ToPathKey(startCell, destCell, levelSize, travelType);
                var pathCacheHit = PathCache.ContainsPath(pathKey);
                if (!pathCacheHit) {
                    // Only tag if no request is already queued for this key.
                    // Otherwise we would re-emit every frame for every unsatisfied agent and
                    // burn CPU in RequestPathsJob filtering duplicates.
                    if (!PathInFlightKeys.Contains(pathKey)) {
                        ECB.AddComponent(sortKey, entity, new MissingPathData {
                            Start = startCell,
                            Dest = destCell,
                            LevelSize = levelSize,
                            TravelType = travelType,
                        });
                    }
                    return;
                }

                progress.IsAttachedToPath = true;
                progress.PathKey = pathKey;
                progress.NodeIndex = -1;
            }

            // Follow current path
            if (progress.IsAttachedToPath) {

                // Check destination hasn't changed
                if (!CacheKeys.DestMatchesPathKey(destCell, levelSize, progress.PathKey)) {
                    progress.IsAttachedToPath = false;
                    return;
                }

                // Check path has been calculated
                var foundInCache = PathCache.TryGetPath(progress.PathKey, out var path);
                if (!foundInCache) {
                    progress.IsAttachedToPath = false;
                    return;
                }

                // Wait for path to generate...
                if (path.IsPending) {
                    return;
                }

                // Check path is not empty
                if (!path.PathWasFound) {
                    result.PathIsImpossible = true;

                    // Invalidate empty path after graph update
                    if (path.GraphVersionAtSearch != Graph.GraphVersion.Value) {
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                        progress.IsAttachedToPath = false;
                        return;
                    }
                    return;
                }

                // Check for sector change
                var nodeIsValid = false;
                int versionCheckDistance = 1;
                if (progress.NodeIndex >= 0 && progress.NodeIndex < path.Nodes.Length) {
                    var node = path.Nodes[progress.NodeIndex];
                    var nodeCell = node.Position.Cell;
                    var nodeMap = Graph.CellToSectorMap(nodeCell, travelType);
                    var newIsland = nodeMap.GetCellIsland(nodeCell);
                    nodeIsValid = nodeMap.Index == startMap.Index && newIsland == startIsland;
                }

                // Connect to a sector
                if (!nodeIsValid) {
                    versionCheckDistance = 3;

                    // Try checking next sector
                    if (progress.NodeIndex < path.Nodes.Length - 1) {
                        var newIndex = progress.NodeIndex + 1;
                        var newNode = path.Nodes[newIndex];
                        var newCell = newNode.Position.Cell;
                        var newMap = Graph.CellToSectorMap(newCell, travelType);
                        var newIsland = newMap.GetCellIsland(newCell);
                        if (newMap.Index == startMap.Index && newIsland == startIsland) {
                            progress.NodeIndex = newIndex;
                            nodeIsValid = true;
                        }
                    }

                    // Try checking all sectors
                    if (!nodeIsValid) {
                        for (int index = 0; index < path.Nodes.Length; index++) {
                            var newNode = path.Nodes[index];
                            var newCell = newNode.Position.Cell;
                            var newMap = Graph.CellToSectorMap(newCell, travelType);
                            var newIsland = newMap.GetCellIsland(newCell);
                            if (newMap.Index == startMap.Index && newIsland == startIsland) {
                                progress.NodeIndex = index;
                                nodeIsValid = true;
                                break;
                            }
                        }
                    }

                    // We are too far from the path. Detach!
                    if (!nodeIsValid) {
                        progress.IsAttachedToPath = false;
                        return;
                    }

                }

                // Check path version
                int minVersionCheck = math.max(progress.NodeIndex, 0);
                int maxVersionCheck = math.min(progress.NodeIndex + versionCheckDistance, path.Nodes.Length);
                for (int i = minVersionCheck; i < maxVersionCheck; i++) {
                    var checkNode = path.Nodes[i];
                    var checkSector = Graph.CellToSector(checkNode.Position.Cell);
                    if (checkSector.Version != checkNode.Version) {

                        // Invalidate old paths
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                        progress.IsAttachedToPath = false;
                        return;
                    }
                }

                // Generate or retrieve a flow
                var pathIndex = progress.NodeIndex;
                var pathNode = path.Nodes[pathIndex];
                var flowKey = pathNode.FlowCacheKey(travelType);
                var flowCacheHit = FlowCache.TryGetField(flowKey, out var flow);
                var cell = pathNode.Position.Cell;
                if (!flowCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingFlowData {
                        SectorIndex = Graph.Layout.CellToSectorIndex(cell),
                        Cell = cell,
                        Direction = pathNode.Direction,
                        TravelType = travelType,
                    });
                    return;
                }

                // Wait for flow to generate...
                if (flow.IsPending) {
                    return;
                }

                // Check flow version
                var flowMap = Graph.CellToSector(pathNode.Position.Cell);
                if (flow.FlowField.Version != flowMap.Version) {
                    progress.IsAttachedToPath = false;
                    return;
                }

                // Find the flow direction
                progress.IsAttachedToFlow = true;
                progress.FlowKey = flowKey;

                var cornerCell = Graph.Layout.GetMinCorner(flowMap.Index);
                var flowDir = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, smoothPos);
                var smoothing = goal.ValueRO.SmoothingMode;

                result.Direction = flowDir;

                // Apply smoothing: Lookahead one tile
                if (smoothing != PathSmoothingMode.None) {
                    var nextPos = pos + result.Direction;
                    var newFlowDir = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, nextPos);
                    if (!newFlowDir.Equals(pos)) {
                        result.Direction = math.normalizesafe(flowDir + newFlowDir);
                    }
                }

                // Apply smoothing: Line of sight
                if (smoothing == PathSmoothingMode.LineOfSight) {

                    // Read previous line of sight results from cache
                    var version = Graph.GraphVersion.Value;
                    var key1 = progress.NewSightlineKey;
                    var key2 = progress.KnownSightlineKey;
                    progress.NewSightlineKey = -1;
                    progress.KnownSightlineKey = -1;

                    if (LineCache.TryGetSightline(key1, version, out var line1) && line1.WasFound) {
                        var cell1 = CacheKeys.ToDestCell(key1, levelSize);
                        result.Direction = math.normalizesafe(cell1 - smoothPos);
                    } else if (LineCache.TryGetSightline(key2, version, out var line2) && line2.WasFound) {
                        var cell2 = CacheKeys.ToDestCell(key2, levelSize);
                        result.Direction = math.normalizesafe(cell2 - smoothPos);
                    }

                    // Queue new line of sight calculations
                    var maxNode = math.min(
                        pathIndex + PathfindingConstants.MAX_LINE_OF_SIGHT_LOOKAHEAD,
                        path.Nodes.Length);

                    for (int n = pathIndex; n < maxNode; n++) {
                        var node = path.Nodes[n];
                        var nodeGoal = node.GoalBounds;
                        var bestDistanceSq = float.MaxValue;
                        var anyNodeFound = false;

                        // Disable if too close to goal - avoids weird bends
                        var margin = 1;
                        if (n == path.Nodes.Length - 1) margin = 0;
                        if (nodeGoal.ContainsCell(pos, margin)) {
                            continue;
                        }

                        // Check line of sight against each cell in the nodes's goal bounds
                        for (int i = nodeGoal.MinCell.x; i <= nodeGoal.MaxCell.x; i++) {
                            for (int j = nodeGoal.MinCell.y; j <= nodeGoal.MaxCell.y; j++) {

                                // We only want the shortest open line 
                                var target = new int2(i, j);
                                var distSq = math.distancesq(pos, target);
                                if (distSq > bestDistanceSq) {
                                    continue;
                                }

                                // Checked cached line of sight result
                                var losKey = CacheKeys.ToPathKey(pos, target, levelSize, travelType);
                                var cacheHit = LineCache.TryGetSightline(losKey, version, out var sightline);

                                if (cacheHit) {
                                    if (sightline.WasFound) {
                                        progress.KnownSightlineKey = losKey;
                                        bestDistanceSq = distSq;
                                        anyNodeFound = true;
                                    }
                                    continue;
                                }

                                // Cache miss - Request the sightline data and stop!
                                else {
                                    progress.NewSightlineKey = losKey;
                                    ECB.AddComponent(sortKey, entity, new MissingSightlineData {
                                        Start = pos,
                                        End = target,
                                        LevelSize = levelSize,
                                        TravelType = travelType,
                                    });
                                    return;
                                }
                            }
                        }

                        // Stop for now!
                        if (!anyNodeFound) {
                            break;
                        }
                    }
                }

            }

        }

    }

}

