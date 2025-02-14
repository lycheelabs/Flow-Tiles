
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
                /*if (path.NoPathExists) {
                    progress.HasPath = false;

                    // Invalidate failed paths on graph change
                    if (Graph.GraphVersion.Value > path.GraphVersionAtSearch) {
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                    }
                    return;
                }*/

                // Wait for path to generate...
                if (path.IsPending) {
                    return;
                }

                var currentIsland = currentMap.GetCellIsland(current);
                
                // Check for sector change
                var nodeIsValid = false;
                int versionCheckDistance = 1;
                if (progress.NodeIndex >= 0 && progress.NodeIndex < path.Nodes.Length) {
                    var node = path.Nodes[progress.NodeIndex];
                    var nodeCell = node.Position.Cell;
                    var nodeMap = Graph.CellToSectorMap(nodeCell, travelType);
                    var newIsland = nodeMap.GetCellIsland(nodeCell);
                    nodeIsValid = nodeMap.Index == currentMap.Index && newIsland == currentIsland;
                }

                // Connect to a sector
                if (!nodeIsValid) {
                    versionCheckDistance = 3;

                    // Default: Check next sector
                    if (progress.NodeIndex < path.Nodes.Length - 1) {
                        var newIndex = progress.NodeIndex + 1;
                        var newNode = path.Nodes[newIndex];
                        var newCell = newNode.Position.Cell;
                        var newMap = Graph.CellToSectorMap(newCell, travelType);
                        var newIsland = newMap.GetCellIsland(newCell);
                        if (newMap.Index == currentMap.Index && newIsland == currentIsland) {
                            progress.NodeIndex = newIndex;
                            nodeIsValid = true;
                        }
                    }

                    // Fallback: Check all sectors
                    if (!nodeIsValid) {
                        for (int index = 0; index < path.Nodes.Length; index++) {
                            var newNode = path.Nodes[index];
                            var newCell = newNode.Position.Cell;
                            var newMap = Graph.CellToSectorMap(newCell, travelType);
                            var newIsland = newMap.GetCellIsland(newCell);
                            if (newMap.Index == currentMap.Index && newIsland == currentIsland) {
                                progress.NodeIndex = index;
                                nodeIsValid = true;
                                break;
                            }
                        }
                    }

                    // Fallback: Cancel path
                    if (!nodeIsValid) {
                        progress.HasPath = false;
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
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                        progress.HasPath = false;
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
                    int2 visiblePos = pos;

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
                                var cacheHit = LineCache.TryGetSightline(losKey, out var sightline);

                                if (cacheHit) {
                                    if (sightline.WasFound) {
                                        visiblePos = goalCell;
                                        bestDistanceSq = distSq;
                                        anyNodeFound = true;
                                    }
                                    continue;
                                }

                                // Cache miss - Request the sightline data and stop!
                                else {
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

                    if (!visiblePos.Equals(pos)) {
                        var direction = math.normalizesafe(visiblePos - smoothPos);
                        result.Direction = direction;
                    }
                }

            }
        }

    }

}

