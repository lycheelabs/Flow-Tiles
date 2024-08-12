
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
            var currentColor = currentMap.GetCellColor(current);
            var currentIsland = currentMap.GetCellIsland(current);

            var destMap = Graph.CellToSectorMap(dest, travelType);
            var destColor = destMap.GetCellColor(dest);
            var destKey = Graph.Layout.IndexOfCell(dest);

            // Attach to a path
            if (!progress.HasPath) {

                // Find closest start portal
                var start = current;
                var startCluster = currentMap.GetRootPortal(current);
                var startKeyCell = startCluster.Position.Cell;
                    
                if (currentMap.Index != destMap.Index || currentColor != destColor) {
                    if (currentMap.Portals.TryGetClosestExitPortal(current, dest, startCluster.Color, out var closest)) {
                        start = closest.Position.Cell;
                        startKeyCell = start;
                    }
                }

                // Generate or retrieve a path
                var startKey = Graph.Layout.IndexOfCell(startKeyCell);
                var pathKey = new int4(startKey, currentColor, destKey, travelType);
                var pathCacheHit = PathCache.ContainsPath(pathKey);
                if (!pathCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingPathData {
                        Start = start,
                        Dest = dest,
                        TravelType = travelType,
                        Key = pathKey,
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
                if (destKey != progress.PathKey.z || travelType != progress.PathKey.w) {
                    progress.HasPath = false;
                    return;
                }

                // Check path exists
                var pathFound = PathCache.TryGetPath(progress.PathKey, out var path);
                if (!pathFound || path.NoPathExists) {
                    progress.HasPath = false;
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
                    var foundNode = false;
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
                            foundNode = true;
                        }
                    }

                    // Fallback: Check all sectors
                    if (!foundNode) {
                        for (int index = 0; index < path.Nodes.Length; index++) {
                            var newNode = path.Nodes[index];
                            var newCell = newNode.Position.Cell;
                            var newMap = Graph.CellToSectorMap(newCell, travelType);
                            var newIsland = newMap.GetCellIsland(newCell);
                            if (newMap.Index == currentMap.Index && newIsland == currentIsland) {
                                progress.NodeIndex = index;
                                foundNode = true;
                                break;
                            }
                        }
                    }

                    // Fallback: Cancel path
                    if (!foundNode) {
                        progress.HasPath = false;
                        return;
                    }

                }

                // Check path version
                int maxVersionCheck = math.min(progress.NodeIndex + versionCheckDistance, path.Nodes.Length);
                for (int i = progress.NodeIndex; i < maxVersionCheck; i++) {
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
                var flowKey = pathNode.CacheKey(travelType);
                var flowCacheHit = FlowCache.TryGetField(flowKey, out var flow);
                var cell = pathNode.Position.Cell;
                if (!flowCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingFlowData {
                        SectorIndex = Graph.Layout.CellToSectorIndex(cell),
                        Cell = cell,
                        Direction = pathNode.Direction,
                        TravelType = travelType,
                        Key = flowKey,
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
                var pos = position.ValueRO.Position - cornerCell;
                var direction = flow.FlowField.GetFlow(pos.x, pos.y);
                result.Direction = direction;

            }

        }
    }

}

