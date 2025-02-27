
using FlowTiles.PortalPaths;
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
            progress.HasFlow = false;

            // Check dest has been set
            if (!goal.ValueRO.HasGoal) {
                progress.HasPath = false;
                return;
            }

            var current = position.ValueRO.PositionCell;
            var dest = goal.ValueRO.Goal;
            var levelSize = Graph.Bounds.SizeCells;
            var travelType = goal.ValueRO.TravelType;
            
            // Check start and dest are valid
            if (!Graph.Bounds.ContainsCell(current) || !Graph.Bounds.ContainsCell(dest)) {
                progress.HasPath = false;
                return;
            }

            // Check path exists
            var currentMap = Graph.CellToSectorMap(current, travelType);
            var currentContinent = currentMap.GetRoot(current).Continent;

            var destMap = Graph.CellToSectorMap(dest, travelType);
            var destContinent = destMap.GetRoot(dest).Continent;

            if (currentContinent != destContinent) {
                progress.HasPath = false;
                return;
            }

            // Attach to a path
            if (!progress.HasPath) {

                // Generate or retrieve a path
                var pathKey = CacheKeys.ToPathKey(current, dest, levelSize, travelType);
                var pathCacheHit = PathCache.ContainsPath(pathKey);
                if (!pathCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingPathData {
                        Start = current,
                        Dest = dest,
                        LevelSize = levelSize,
                        TravelType = travelType,
                    });
                    return;
                }

                progress.HasPath = true;
                progress.PathKey = pathKey;
                progress.NodeIndex = -1;
            }

            // Follow current path
            if (progress.HasPath) {

                // Check destination hasn't changed
                if (!CacheKeys.DestMatchesPathKey(dest, levelSize, progress.PathKey)) {
                    progress.HasPath = false;
                    return;
                }

                // Check path exists
                var pathFound = PathCache.TryGetPath(progress.PathKey, out var path);
                if (!pathFound) {
                    progress.HasPath = false;
                    return;
                }

                // Wait for path to generate...
                if (path.IsPending) {
                    return;
                }

                // Check version of the starting node
                var firstNode = path.Nodes[0];
                var firstSector = Graph.CellToSector(firstNode.Position.Cell);
                if (firstNode.Version != firstSector.Version) {
                    progress.HasPath = false;

                    // Remove this path from the cache
                    ECB.AddComponent(sortKey, entity, new InvalidPathData {
                        Key = progress.PathKey,
                    });
                    return;
                }


                // Check for island change
                var currentIsland = currentMap.GetCellIsland(current);
                var currentIslandFound = false;
                var versionCheckRange = 1;

                if (progress.NodeIndex >= 0 && progress.NodeIndex < path.Nodes.Length) {
                    var node = path.Nodes[progress.NodeIndex];
                    var nodeCell = node.Position.Cell;
                    var nodeMap = Graph.CellToSectorMap(nodeCell, travelType);
                    var newIsland = nodeMap.GetCellIsland(nodeCell);
                    var currentSectorFound = nodeMap.Index == currentMap.Index;
                    currentIslandFound = currentSectorFound && newIsland == currentIsland;
                }

                // On island change, search path for current island
                if (!currentIslandFound) {

                    // Extend version checks when the island changes
                    versionCheckRange = 3; 

                    // Check the expected (next) island
                    if (progress.NodeIndex < path.Nodes.Length - 1) {
                        var newIndex = progress.NodeIndex + 1;
                        var newNode = path.Nodes[newIndex];
                        var newCell = newNode.Position.Cell;
                        var newMap = Graph.CellToSectorMap(newCell, travelType);
                        var newIsland = newMap.GetCellIsland(newCell);

                        var currentSectorFound = newMap.Index == currentMap.Index;
                        currentIslandFound = currentSectorFound && newIsland == currentIsland;

                        if (currentIslandFound) {
                            progress.NodeIndex = newIndex;
                            currentSectorFound = true;
                        }
                    }

                    // Fallback: Check all islands
                    if (!currentIslandFound) {
                        for (int index = 0; index < path.Nodes.Length; index++) {
                            var newNode = path.Nodes[index];
                            var newCell = newNode.Position.Cell;
                            var newMap = Graph.CellToSectorMap(newCell, travelType);
                            var newIsland = newMap.GetCellIsland(newCell);

                            var currentSectorFound = newMap.Index == currentMap.Index;
                            currentIslandFound = currentSectorFound && newIsland == currentIsland;

                            if (currentIslandFound) {
                                progress.NodeIndex = index;
                                break;
                            }
                        }
                    }

                    // If the path doesn't contain my island, cancel the path
                    if (!currentIslandFound) {
                        progress.HasPath = false;
                        return;
                    }

                }

                // Check version of the current and nearby node
                int minVersionCheck = math.max(progress.NodeIndex, 0);
                int maxVersionCheck = math.min(progress.NodeIndex + versionCheckRange, path.Nodes.Length);
                for (int i = minVersionCheck; i < maxVersionCheck; i++) {
                    var checkNode = path.Nodes[i];
                    var checkSector = Graph.CellToSector(checkNode.Position.Cell);
                    if (checkSector.Version != checkNode.Version) {
                        progress.HasPath = false;

                        // Invalidate the path
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
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
                    progress.HasPath = false;
                    return;
                }

                // Find the flow direction
                progress.HasFlow = true;
                progress.FlowKey = flowKey;

                var cornerCell = Graph.Layout.GetMinCorner(currentMap.Index);
                var smoothPos = position.ValueRO.Position;
                var pos = position.ValueRO.PositionCell;
                var flowDirection = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, smoothPos);

                result.Direction = flowDirection;
                if (pos.Equals(dest)) {
                    return;
                }

                var smoothing = goal.ValueRO.SmoothingMode;

                // Lookahead one tile smoothing
                if (smoothing != PathSmoothingMode.None) {
                    var nextPos = pos + result.Direction;
                    var nextFlowDirection = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, nextPos);
                    if (!nextFlowDirection.Equals(pos)) {
                        result.Direction = math.normalizesafe(flowDirection + nextFlowDirection);
                    }
                }

                // Line of sight smoothing
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
                    }
                    else if (LineCache.TryGetSightline(key2, version, out var line2) && line2.WasFound) {
                        var cell2 = CacheKeys.ToDestCell(key2, levelSize);
                        result.Direction = math.normalizesafe(cell2 - smoothPos);
                    }

                    // Queue new line of sight calculations
                    var maxNode = math.min(
                        pathIndex + Constants.MAX_LINE_OF_SIGHT_LOOKAHEAD,
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
                                var goalCell = new int2(i, j);
                                var distSq = math.distancesq(pos, goalCell);
                                if (distSq > bestDistanceSq) {
                                    continue;
                                }

                                // Checked cached line of sight result
                                var losKey = CacheKeys.ToPathKey(pos, goalCell, levelSize, travelType);
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
                                        End = goalCell,
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

