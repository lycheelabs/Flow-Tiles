
using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct PathfindingSystem : ISystem {

        public const int CACHE_CAPACITY = 1000;

        private PathCache PathCache;
        private FlowCache FlowCache;

        //private Entity BufferSingleton;
        private NativeList<int> RebuildRequests;
        private NativeList<PathRequest> PathRequests;
        private NativeList<FlowRequest> FlowRequests;

        //private JobHandle RebuildJob;

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
            RebuildRequests = new NativeList<int>(50, Allocator.Persistent);
            PathRequests = new NativeList<PathRequest>(200, Allocator.Persistent);
            FlowRequests = new NativeList<FlowRequest>(200, Allocator.Persistent);

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            
            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var level = globalData.Level;
            var graph = globalData.Graph;
            
            // Rebuild all dirty graph sectors
            if (level.NeedsRebuilding) {
                PrepareForRebuild(ref level, ref graph);
                level.NeedsRebuilding = false;

                var job = new RebuildGraphJob {
                    Requests = RebuildRequests,
                    Graph = graph,
                };
                state.Dependency = job.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);
            }

            // Process pathfinding requests, and cache the results
            foreach (var request in PathRequests) {

                // Discard duplicate requests
                if (PathCache.Cache.TryGetValue(request.cacheKey, out var existing) && !existing.IsPending) {
                    continue;
                }

                // Calculate the path
                var origin = request.originCell;
                var dest = request.destCell;
                var success = PortalPathJob.ScheduleAndComplete(graph, origin, dest, out var path);

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
                var goalMap = graph.CellToSectorMap(goal, travelType: 0);
                var goalBounds = new CellRect(goal, goal);

                if (!request.goalDirection.Equals(0)) {
                    if (goalMap.TryGetExitPortal(goal, out var portal)) {
                        goalBounds = portal.Bounds;
                    }
                }

                // Calculate the flow tile
                var goalDir = request.goalDirection;
                var flow = CalculateFlowJob.ScheduleAndComplete(goalMap.Costs, goalBounds, goalDir);

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
                Graph = graph,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                ECB = ecbLate.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            new DebugPathsJob {
                FlowCache = FlowCache.Cache,
            }.ScheduleParallel();

            globalData.Level = level;
            SystemAPI.SetSingleton(globalData);

        }

        private void PrepareForRebuild(ref PathableLevel level, ref PathableGraph graph) {
            RebuildRequests.Clear();
            for (int index = 0; index < graph.Layout.NumSectorsInLevel; index++) {
                if (level.SectorFlags[index].NeedsRebuilding) {
                    graph.ReinitialiseSector(index, level);
                    level.SectorFlags[index] = default;
                    RebuildRequests.Add(index);
                }
            }
            for (int index = 0; index < RebuildRequests.Length; index++) {
                graph.BuildSectorExits(index);
            }
        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

    }

}