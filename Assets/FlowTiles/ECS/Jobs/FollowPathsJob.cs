
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

            /*result.Direction = 0;

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

            var startSector = Graph.Costs.GetSectorIndex(current.x, current.y);
            var startColor = Graph.Costs.GetColor(current.x, current.y);
            var destSector = Graph.Costs.GetSectorIndex(dest.x, dest.y);
            var destColor = Graph.Costs.GetColor(dest.x, dest.y);
            var destKey = Graph.Costs.GetCellIndex(dest.x, dest.y);

            // Attach to a path
            if (!progress.HasPath) {

                // Find closest start portal
                var start = current;
                var startCluster = Graph.GetRootPortal(current.x, current.y);
                var startKeyCell = startCluster.Position.Cell;
                    
                if (startSector != destSector || startColor != destColor) {
                    var sectorData = Graph.Portals.Sectors[startSector];
                    if (sectorData.TryGetClosestExitPortal(current, startCluster.Color, out var closest)) {
                        start = closest.Position.Cell;
                        startKeyCell = start;
                    }
                }

                // Search for a cached path
                var startKey = Graph.Costs.GetCellIndex(startKeyCell.x, startKeyCell.y);
                var pathKey = new int4(startKey, startColor, destKey, destColor);
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
                progress.NodeIndex = 0;
            }

            // Read current path
            if (progress.HasPath) {

                // Check destination hasn't changed
                if (destKey != progress.PathKey.z || destColor != progress.PathKey.w) {
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
                if (progress.NodeIndex >= 0 && progress.NodeIndex < path.Nodes.Length) {
                    var node = path.Nodes[progress.NodeIndex];
                    var nodeCell = node.Position.Cell;
                    var nodeSector = Graph.Costs.GetSectorIndex(nodeCell.x, nodeCell.y);
                    var nodeColor = Graph.Costs.GetColor(nodeCell.x, nodeCell.y);
                    nodeIsValid = nodeSector == startSector && nodeColor == startColor;
                }
                if (!nodeIsValid) {
                    var foundNode = false;

                    // Check next sector
                    if (progress.NodeIndex < path.Nodes.Length - 1) {
                        var newIndex = progress.NodeIndex + 1;
                        var newNode = path.Nodes[newIndex];
                        var newCell = newNode.Position.Cell;
                        var newSector = Graph.Costs.GetSectorIndex(newCell.x, newCell.y);
                        var newColor = Graph.Costs.GetColor(newCell.x, newCell.y);
                        if (newSector == startSector && newColor == startColor) {
                            progress.NodeIndex = newIndex;
                            foundNode = true;
                        }
                    }

                    // Fallback: Check all sectors
                    if (!foundNode) {
                        for (int index = 0; index < path.Nodes.Length; index++) {
                            var newNode = path.Nodes[index];
                            var newCell = newNode.Position.Cell;
                            var newSector = Graph.Costs.GetSectorIndex(newCell.x, newCell.y);
                            var newColor = Graph.Costs.GetColor(newCell.x, newCell.y);
                            if (newSector == startSector && newColor == startColor) {
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

                // Search for a cached flow
                var pathNode = path.Nodes[progress.NodeIndex];
                var flowKey = pathNode.CacheKey;
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

                progress.HasFlow = true;
                progress.FlowKey = flowKey;

                // Save the flow direction
                var pos = position.ValueRO.Position - Graph.Costs.Sectors[startSector].Bounds.MinCell;
                var direction = flow.FlowField.GetFlow(pos.x, pos.y);
                result.Direction = direction;


            }*/

        }
    }

}

