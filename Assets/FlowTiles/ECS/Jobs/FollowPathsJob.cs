
using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct FollowPathsJob : IJobEntity {

        [ReadOnly] public PathableGraph Graph;
        [ReadOnly] public NativeParallelHashMap<int4, CachedPortalPath> PathCache;
        [ReadOnly] public NativeParallelHashMap<int4, CachedFlowField> FlowCache;

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

            if (!goal.ValueRO.HasGoal) {
                progress.HasPath = false;
                progress.HasFlow = false;
                return;
            }

            // Check start and dest are valid
            var current = position.ValueRO.Position;
            var dest = goal.ValueRO.Goal;
            if (!Graph.Bounds.ContainsCell(current) || !Graph.Bounds.ContainsCell(dest)) {
                progress.HasPath = false;
                progress.HasFlow = false;
                return;
            }

            var travelType = 0;
            var startMap = Graph.CellToSectorMap(current, travelType);
            var startColor = startMap.GetCellColor(current);
            var destMap = Graph.CellToSectorMap(dest, travelType);
            var destColor = destMap.GetCellColor(dest);
            var destKey = Graph.Layout.IndexOfCell(dest);

            // Attach to a path
            if (!progress.HasPath) {

                // Find closest start portal
                var start = current;
                var startCluster = startMap.GetRootPortal(current);
                var startKeyCell = startCluster.Position.Cell;
                    
                if (startMap.Index != destMap.Index || startColor != destColor) {
                    if (startMap.Portals.TryGetClosestExitPortal(current, startCluster.Color, out var closest)) {
                        start = closest.Position.Cell;
                        startKeyCell = start;
                    }
                }

                // Generate or retrieve a path
                var startKey = Graph.Layout.IndexOfCell(startKeyCell);
                var pathKey = new int4(startKey, startColor, destKey, travelType);
                var pathCacheHit = PathCache.ContainsKey(pathKey);
                if (!pathCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingPathData {
                        Start = start,
                        Dest = dest,
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
                    progress.HasFlow = false;
                    return;
                }

                // Check path exists
                var pathFound = PathCache.TryGetValue(progress.PathKey, out var path);
                if (!pathFound || path.NoPathExists) {
                    progress.HasPath = false;
                    progress.HasFlow = false;
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
                    var nodeColor = nodeMap.GetCellColor(nodeCell);
                    nodeIsValid = nodeMap.Index == startMap.Index && nodeColor == startColor;
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
                        var newColor = newMap.GetCellColor(newCell);
                        if (newMap.Index == startMap.Index && newColor == startColor) {
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
                            var newColor = newMap.GetCellColor(newCell);
                            if (newMap.Index == startMap.Index && newColor == startColor) {
                                progress.NodeIndex = index;
                                foundNode = true;
                                break;
                            }
                        }
                    }

                    // Fallback: Cancel path
                    if (!foundNode) {
                        progress.HasPath = false;
                        progress.HasFlow = false;
                        return;
                    }

                }

                // Check path version
                int maxVersionCheck = math.min(progress.NodeIndex + versionCheckDistance, path.Nodes.Length);
                for (int i = progress.NodeIndex; i < maxVersionCheck; i++) {
                    var checkNode = path.Nodes[i];
                    var checkSector = Graph.CellToSector(checkNode.Position.Cell, travelType);
                    if (checkSector.Version != checkNode.Version) {
                        ECB.AddComponent(sortKey, entity, new InvalidPathData {
                            Key = progress.PathKey,
                        });
                        progress.HasPath = false;
                        progress.HasFlow = false;
                        return;
                    }
                }

                // Generate or retrieve a flow
                var pathNode = path.Nodes[progress.NodeIndex];
                var flowKey = pathNode.CacheKey(travelType);
                var flowCacheHit = FlowCache.TryGetValue(flowKey, out var flow);
                if (!flowCacheHit) {
                    ECB.AddComponent(sortKey, entity, new MissingFlowData {
                        Cell = pathNode.Position.Cell,
                        Direction = pathNode.Direction,
                        Key = flowKey,
                    });
                    return;
                }

                // Wait for flow to generate...
                if (flow.IsPending) {
                    progress.HasFlow = false;
                    return;
                }

                // Check flow version
                var flowMap = Graph.CellToSector(pathNode.Position.Cell, travelType);
                if (flow.FlowField.Version != flowMap.Version) {
                    ECB.AddComponent(sortKey, entity, new InvalidFlowData {
                        Key = flowKey,
                    });
                    progress.HasPath = false;
                    progress.HasFlow = false;
                    return;
                }

                // Find the flow direction
                progress.HasFlow = true;
                progress.FlowKey = flowKey;
                var cornerCell = Graph.Layout.GetMinCorner(startMap.Index);
                var pos = position.ValueRO.Position - cornerCell;
                var direction = flow.FlowField.GetFlow(pos.x, pos.y);
                result.Direction = direction;

            }

        }
    }

}

