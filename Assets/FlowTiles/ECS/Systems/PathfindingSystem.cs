using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace FlowTiles.ECS {

    public partial struct PathfindingSystem : ISystem {

        public const int CACHE_CAPACITY = 1000;

        private PathCache PathCache;
        private FlowCache FlowCache;

        //private Entity BufferSingleton;
        private NativeList<PathRequest> PathRequests;
        private NativeList<FlowRequest> FlowRequests;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the caches
            PathCache = new PathCache {
                Cache = new NativeParallelHashMap<int4, CachedPortalPath>(CACHE_CAPACITY, Allocator.Persistent) 
            };
            FlowCache = new FlowCache {
                Cache = new NativeParallelHashMap<int4, CachedFlowField>(CACHE_CAPACITY, Allocator.Persistent)
            };

            // Build the request buffers
            PathRequests = new NativeList<PathRequest>(200, Allocator.Persistent);
            FlowRequests = new NativeList<FlowRequest>(200, Allocator.Persistent);

        }

        public void OnUpdate(ref SystemState state) {

            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var pathGraph = globalData.Graph;

            // Process pathfinding requests, and cache the results
            foreach (var request in PathRequests) {

                // Discard duplicate requests
                if (PathCache.Cache.TryGetValue(request.cacheKey, out var existing) && !existing.IsPending) {
                    continue;
                }

                // Calculate the path
                var origin = request.originCell;
                var dest = request.destCell;
                var success = PortalPathJob.ScheduleAndComplete(pathGraph, origin, dest, out var path);

                // Cache the path
                PathCache.Cache[request.cacheKey] = new CachedPortalPath {
                    IsPending = false,
                    NoPathExists = !success,
                    Nodes = path
                };
            }
            PathRequests.Clear();

            // Process flow field requests, and cache the results
            foreach (var request in FlowRequests) {

                // Discard duplicate requests
                if (FlowCache.Cache.TryGetValue(request.cacheKey, out var existing) && !existing.IsPending) {
                    continue;
                }

                // Find the goal boundaries
                var goal = request.goalCell;
                var goalBounds = new CellRect(goal, goal);
                if (!request.goalDirection.Equals(0)) {
                    if (pathGraph.TryGetExitPortal(goal.x, goal.y, out var portal)) {
                        goalBounds = portal.Bounds;
                    }
                }

                // Calculate the flow tile
                var sector = pathGraph.GetCostSector(goal.x, goal.y);
                var goalDir = request.goalDirection;
                var flow = FlowFieldJob.ScheduleAndComplete(sector, goalBounds, goalDir);

                FlowCache.Cache[request.cacheKey] = new CachedFlowField { 
                    IsPending = false,
                    FlowField = flow
                };
            }
            FlowRequests.Clear();

            var ecbEarly = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecbLate = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            new RequestPathsJob {
                PathCache = PathCache.Cache,
                PathRequests = PathRequests,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            new RequestFlowsJob {
                FlowCache = FlowCache.Cache,
                FlowRequests = FlowRequests,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            new FollowPathsJob {
                Graph = pathGraph,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                ECB = ecbLate.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            new DebugPathsJob {
                FlowCache = FlowCache.Cache,
            }.ScheduleParallel();

        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public NativeParallelHashMap<int4, CachedPortalPath> PathCache;
            public NativeList<PathRequest> PathRequests;

            public EntityCommandBuffer ECB;

            [BurstCompile]
            private void Execute(MissingPathData data, Entity entity) {
                var key = data.Key;
                if (!PathCache.ContainsKey(key)) {

                    // Request a path be generated
                    PathRequests.Add(new PathRequest {
                        originCell = data.Start,
                        destCell = data.Dest,
                        cacheKey = key,
                    });

                    // Store temp data in the cache
                    PathCache[key] = new CachedPortalPath {
                        IsPending = true
                    };

                    // Remove component
                    ECB.RemoveComponent<MissingPathData>(entity);

                }
            }
        }

        [BurstCompile]
        public partial struct RequestFlowsJob : IJobEntity {

            public NativeParallelHashMap<int4, CachedFlowField> FlowCache;
            public NativeList<FlowRequest> FlowRequests;

            public EntityCommandBuffer ECB;

            [BurstCompile]
            private void Execute(MissingFlowData data, Entity entity) {
                var key = data.Key;
                if (!FlowCache.ContainsKey(key)) {

                    // Request a flow be generated
                    FlowRequests.Add(new FlowRequest {
                        goalCell = data.Cell,
                        goalDirection = data.Direction,
                    });

                    // Store temp data in the cache
                    FlowCache[key] = new CachedFlowField {
                        IsPending = true
                    };

                    // Remove component
                    ECB.RemoveComponent<MissingFlowData>(entity);

                }
            }
        }

        [BurstCompile]
        public partial struct FollowPathsJob : IJobEntity {

            [ReadOnly] public PathableGraph Graph;
            [ReadOnly] public NativeParallelHashMap<int4, CachedPortalPath> PathCache;
            [ReadOnly] public NativeParallelHashMap<int4, CachedFlowField> FlowCache;

            public EntityCommandBuffer.ParallelWriter ECB;

            [BurstCompile]
            private void Execute(
                    Entity entity,
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
                    var pos = position.Position - Graph.Costs.Sectors[startSector].Bounds.MinCell;
                    var direction = flow.FlowField.GetFlow(pos.x, pos.y);
                    result.Direction = direction;


                }

            }
        }

        [BurstCompile]
        public partial struct DebugPathsJob : IJobEntity {

            public NativeParallelHashMap<int4, CachedFlowField> FlowCache;

            [BurstCompile]
            private void Execute(FlowProgress progress, ref FlowDebugData debug) {
                debug.CurrentFlowTile = default;

                if (!progress.HasFlow) {
                    return;
                }
                var foundFlow = FlowCache.TryGetValue(progress.FlowKey, out var flow);
                if (!foundFlow || flow.IsPending) {
                    return;
                }

                debug.CurrentFlowTile = flow.FlowField;
            }
        }

    }

}