﻿
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

            if (!goal.ValueRO.HasGoal) {
                progress.HasPath = false;
                return;
            }

            // Check start and dest are valid
            var current = position.ValueRO.Position;
            var dest = goal.ValueRO.Goal;
            if (!Graph.Bounds.ContainsCell(current) || !Graph.Bounds.ContainsCell(dest)) {
                progress.HasPath = false;
                return;
            }

            var travelType = goal.ValueRO.TravelType;
            var currentMap = Graph.CellToSectorMap(current, travelType);
            var currentIsland = currentMap.GetCellIsland(current);

            // Attach to a path
            if (!progress.HasPath) {

                // Generate or retrieve a path
                var pathKey = PathCache.ToKey(current, dest, Graph.Bounds.SizeCells, travelType);
                var pathCacheHit = PathCache.ContainsPath(pathKey);
                if (!pathCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingPathData {
                        Start = current,
                        Dest = dest,
                        LevelSize = Graph.Bounds.SizeCells,
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
                if (!PathCache.DestMatchesKey(progress.PathKey, dest, Graph.Bounds.SizeCells)) {
                    progress.HasPath = false;
                    return;
                }

                // Check path exists
                var pathFound = PathCache.TryGetPath(progress.PathKey, out var path);
                if (!pathFound) {
                    progress.HasPath = false;
                    return;
                }
                if (path.NoPathExists) {
                    progress.HasPath = false;

                    // Invalidate failed paths on graph change
                    if (Graph.GraphVersion.Value > path.GraphVersionAtSearch) {
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                    }
                    return;
                }

                // Wait for path to generate...
                if (path.IsPending) {
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
                var pathNode = path.Nodes[progress.NodeIndex];
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
                var pos = position.ValueRO.Position;
                var flowDirection = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, pos);

                result.Direction = flowDirection;
                var smoothing = goal.ValueRO.SmoothingMode;

                // Lookahead one tile smoothing
                if (smoothing == PathSmoothingMode.LookaheadOneTile) {
                    var nextPos = pos + result.Direction;
                    var nextFlowDirection = FlowTileUtils.GetFlowDirection(ref flow, cornerCell, nextPos);
                    if (!nextFlowDirection.Equals(pos)) {
                        result.Direction = math.normalizesafe(flowDirection + nextFlowDirection);
                    }
                }

                // Fast LOS smoothing
                else if (smoothing == PathSmoothingMode.FastLineOfSight) {
                    var direction = FlowTileUtils.GetBestPathLineOfSightDirection(
                        pos, progress.NodeIndex, path.Nodes, ref Graph, travelType);

                    if (!direction.Equals(0)) {
                        result.Direction = direction;
                    }
                }

                // Precise LOS smoothing
                else if (smoothing == PathSmoothingMode.PreciseLineOfSight) {
                    var direction = FlowTileUtils.GetBestPathLineOfSightDirection(
                        pos, progress.NodeIndex, path.Nodes, ref Graph, travelType);

                    if (!direction.Equals(0)) {
                        result.Direction = direction;
                    }
                }
            }
        }

    }

}

