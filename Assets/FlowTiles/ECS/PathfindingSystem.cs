using FlowTiles.FlowField;
using FlowTiles.PortalGraphs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {

    public partial struct PathfindingSystem : ISystem {

        private PathCache PathCache;
        private FlowCache FlowCache;

        //private Entity BufferSingleton;
        private NativeList<PathRequest> PathRequests;
        private NativeList<FlowRequest> FlowRequests;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the caches
            PathCache = new PathCache {
                Cache = new NativeParallelHashMap<int4, PortalPath>(1000, Allocator.Persistent) 
            };
            FlowCache = new FlowCache {
                Cache = new NativeParallelHashMap<int4, FlowFieldTile>(1000, Allocator.Persistent)
            };

            // Build the request buffers
            PathRequests = new NativeList<PathRequest>(1000, Allocator.Persistent);
            FlowRequests = new NativeList<FlowRequest>(1000, Allocator.Persistent);

        }

        public void OnUpdate(ref SystemState state) {

            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var pathGraph = globalData.Graph;

            // Process pathfinding requests, and cache the results
            foreach (var request in PathRequests) {
                //UnityEngine.Debug.Log("Creating path: " + request.cacheKey);

                //var pathfinder = new PortalPathfinder(pathGraph, 200, Allocator.Persistent);
                var success = PortalPathJob.ScheduleAndComplete(
                    pathGraph, request.originCell, request.destCell, out var path);

                PathCache.Cache[request.cacheKey] = new PortalPath {
                    Nodes = path
                };
            }
            PathRequests.Clear();

            // Process flow field requests, and cache the results
            foreach (var request in FlowRequests) {
                //UnityEngine.Debug.Log("Requested flow: " + request.cacheKey);
                var goal = request.goalCell;
                var goalBounds = new CellRect(goal, goal);
                if (!request.goalDirection.Equals(0)) {
                    if (pathGraph.TryGetExitPortal(goal.x, goal.y, out var portal)) {
                        goalBounds = portal.Bounds;
                    }
                }
                var sector = pathGraph.GetCostSector(goal.x, goal.y);
                var goalDir = request.goalDirection;
                var calculator = new FlowCalculator(sector, goalBounds, goalDir);
                FlowCalculator.BurstCalculate(ref calculator);

                FlowCache.Cache[request.cacheKey] = new FlowFieldTile {
                    SectorIndex = sector.Index,
                    Color = calculator.Color,
                    Size = sector.Bounds.SizeCells,
                    Directions = calculator.Flow,
                };
            }
            FlowRequests.Clear();

            //var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            new FollowPathsJob {
                Graph = pathGraph,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                PathRequests = PathRequests,
                FlowRequests = FlowRequests,
            }.Schedule();

            new DebugPathsJob {
                FlowCache = FlowCache.Cache,
            }.Schedule();

        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

        [BurstCompile]
        public partial struct FollowPathsJob : IJobEntity {

            public PathableGraph Graph;
            public NativeParallelHashMap<int4, PortalPath> PathCache;
            public NativeParallelHashMap<int4, FlowFieldTile> FlowCache;

            public NativeList<PathRequest> PathRequests;
            public NativeList<FlowRequest> FlowRequests;

            [BurstCompile]
            private void Execute(
                    FlowPosition position, 
                    FlowGoal goal, 
                    ref FlowProgress progress,
                    ref FlowDirection result, 
                    [ChunkIndexInQuery] int sortKey) {

                result.Direction = 0;

                if (!goal.HasGoal) {
                    progress.HasPath = false;
                    progress.HasFlow = false;
                    return;
                }

                // Check start and dest are valid
                var current = position.Position;
                var dest = goal.Goal;
                if (!Graph.Bounds.ContainsCell(current) || !Graph.Bounds.ContainsCell(dest)) {
                    progress.HasPath = false;
                    progress.HasFlow = false;
                    return;
                }

                var startSector = Graph.Costs.GetSectorIndex(current.x, current.y);
                var startColor = Graph.Costs.GetColor(current.x, current.y);
                var destSector = Graph.Costs.GetSectorIndex(dest.x, dest.y);
                var destColor = Graph.Costs.GetColor(dest.x, dest.y);
                var destCell = Graph.Costs.GetCellIndex(dest.x, dest.y);

                // Attach to a path
                if (!progress.HasPath) {

                    // Find closest start portal
                    var startPortal = Graph.GetRootPortal(current.x, current.y);
                    var start = startPortal.Position.Cell;
               
                    if (startSector != destSector || startColor != destColor) {
                        var sectorData = Graph.Portals.Sectors[startSector];
                        if (sectorData.TryGetClosestExitPortal(current, out var closest)) {
                            start = closest.Position.Cell;
                        }
                    } 
                    var startCell = Graph.Costs.GetCellIndex(start.x, start.y);

                    // Search for a cached path
                    var pathKey = new int4(startCell, startColor, destCell, destColor);
                    var pathCacheHit = PathCache.ContainsKey(pathKey);
                    if (!pathCacheHit) {

                        // Request a path be generated
                        PathRequests.Add(new PathRequest {
                            originCell = start,
                            destCell = dest,
                            cacheKey = pathKey,
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
                    if (destCell != progress.PathKey.z || destColor != progress.PathKey.w) {
                        progress.HasPath = false;
                        progress.HasFlow = false;
                        return;
                    }

                    // Check path still exists
                    var pathFound = PathCache.TryGetValue(progress.PathKey, out var path);
                    if (!pathFound || path.Nodes.Length == 0) {
                        progress.HasPath = false;
                        progress.HasFlow = false;
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
                    var flowCacheHit = FlowCache.ContainsKey(flowKey);
                    if (!flowCacheHit) {

                        // Request a flow be generated
                        FlowRequests.Add(new FlowRequest {
                            goalCell = pathNode.Position.Cell,
                            goalDirection = pathNode.Direction,
                        });
                        progress.HasFlow = false;
                        return;
                    }

                    progress.HasFlow = true;
                    progress.FlowKey = flowKey;

                    // Save the flow direction
                    var pos = position.Position - Graph.Costs.Sectors[startSector].Bounds.MinCell;
                    var flow = FlowCache[flowKey];
                    var direction = flow.GetFlow(pos.x, pos.y);
                    result.Direction = direction;


                }

            }
        }

        [BurstCompile]
        public partial struct DebugPathsJob : IJobEntity {

            public NativeParallelHashMap<int4, FlowFieldTile> FlowCache;

            [BurstCompile]
            private void Execute(FlowProgress progress, ref FlowDebugData debug) {
                debug.CurrentFlowTile = default;

                if (!progress.HasFlow) {
                    return;
                }
                var foundFlow = FlowCache.TryGetValue(progress.FlowKey, out var flow);
                if (!foundFlow) {
                    return;
                }

                debug.CurrentFlowTile = flow;
            }
        }

    }

}