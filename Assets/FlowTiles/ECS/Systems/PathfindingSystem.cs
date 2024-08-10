
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

            var ecbEarly = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecbLate = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var level = globalData.Level;
            var graph = globalData.Graph;
            
            // Rebuild all dirty graph sectors
            if (level.NeedsRebuilding.Value) {
                RebuildDirtySectors(ref level, ref graph, ref state);
            }

            // Process path and flow requests, and cache the results
            ProcessPathRequests(graph);
            ProcessFlowRequests(graph);

            // Invalidate old paths
            new InvalidatePathsJob {
                PathCache = PathCache.Cache,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Invalidate old flows
            new InvalidateFlowsJob {
                FlowCache = FlowCache.Cache,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate path rquests
            new RequestPathsJob {
                PathCache = PathCache.Cache,
                PathRequests = PathRequests,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate flow rquests
            new RequestFlowsJob {
                FlowCache = FlowCache.Cache,
                FlowRequests = FlowRequests,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Follow the paths
            new FollowPathsJob {
                Graph = graph,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                ECB = ecbLate.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            // Visualises pathing data
            new DebugPathsJob {
                FlowCache = FlowCache.Cache,
            }.ScheduleParallel();

        }

        private void ProcessPathRequests(PathableGraph graph) {
            foreach (var request in PathRequests) {

                // Discard duplicate requests
                if (PathCache.Cache.TryGetValue(request.cacheKey, out var existing) && !existing.IsPending) {
                    continue;
                }

                // Calculate the path
                var origin = request.originCell;
                var dest = request.destCell;
                var travelType = request.travelType;
                var success = PortalPathJob.ScheduleAndComplete(graph, origin, dest, travelType, out var path);

                // Cache the path
                PathCache.Cache[request.cacheKey] = new CachedPortalPath {
                    IsPending = false,
                    NoPathExists = !success,
                    Nodes = path
                };
            }
            PathRequests.Clear();
        }

        private void ProcessFlowRequests(PathableGraph graph) {
            foreach (var request in FlowRequests) {

                // Discard duplicate requests
                if (FlowCache.Cache.TryGetValue(request.cacheKey, out var existing) && !existing.IsPending) {
                    continue;
                }

                // Find the goal boundaries
                var goal = request.goalCell;
                var travelType = request.travelType;
                var goalMap = graph.CellToSectorMap(goal, travelType);
                var goalBounds = new CellRect(goal, goal);

                if (!request.goalDirection.Equals(0)) {
                    if (goalMap.TryGetExitPortal(goal, out var portal)) {
                        goalBounds = portal.Bounds;
                    }
                }

                // Calculate the flow tile
                var goalDir = request.goalDirection;
                var flow = CalculateFlowJob.ScheduleAndComplete(goalMap, goalBounds, goalDir);

                FlowCache.Cache[request.cacheKey] = new CachedFlowField {
                    IsPending = false,
                    FlowField = flow
                };
            }
            FlowRequests.Clear();
        }

        private void RebuildDirtySectors(ref PathableLevel level, ref PathableGraph graph, ref SystemState state) {
            RebuildRequests.Clear();
            for (int index = 0; index < graph.Layout.NumSectorsInLevel; index++) {
                if (level.SectorFlags[index].NeedsRebuilding) {
                    graph.ReinitialiseSector(index, level);
                    level.SectorFlags[index] = default;
                    RebuildRequests.Add(index);
                }
            }

            for (int index = 0; index < RebuildRequests.Length; index++) {
                graph.BuildSectorExits(RebuildRequests[index]);
            }

            var job = new RebuildGraphJob {
                Requests = RebuildRequests,
                Graph = graph,
            };
            state.Dependency = job.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);

            level.NeedsRebuilding.Value = false;
        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

    }

}