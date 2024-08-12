
using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        private FindPathsJob PathsJob;
        private FindFlowsJob FlowsJob;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the caches
            PathCache = new PathCache {
                Cache = new NativeParallelHashMap<int4, CachedPortalPath>(CACHE_CAPACITY, Allocator.Persistent) 
            };
            FlowCache = new FlowCache(CACHE_CAPACITY);

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
            ProcessPathRequests(graph, ref state);
            ProcessFlowRequests(graph, ref state);

            // Invalidate old paths
            new InvalidatePathsJob {
                PathCache = PathCache.Cache,
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
                FlowCache = FlowCache,
                FlowRequests = FlowRequests,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Follow the paths
            new FollowPathsJob {
                Graph = graph,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache,
                ECB = ecbLate.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            // Visualises pathing data
            new DebugPathsJob {
                FlowCache = FlowCache,
            }.ScheduleParallel();

        }

        private void RebuildDirtySectors(ref PathableLevel level, ref PathableGraph graph, ref SystemState state) {
            RebuildRequests.Clear();
            for (int index = 0; index < graph.Layout.NumSectorsInLevel; index++) {
                if (level.SectorFlags[index].NeedsRebuilding) {
                    FlowCache.ClearSector(index);
                    graph.ReinitialiseSector(index, level);
                    level.SectorFlags[index] = default;
                    RebuildRequests.Add(index);
                }
            }

            for (int request = 0; request < RebuildRequests.Length; request++) {
                var index = RebuildRequests[request];
                graph.BuildSectorExits(index);
            }

            var job = new RebuildGraphJob {
                Requests = RebuildRequests,
                Graph = graph,
            };
            state.Dependency = job.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);

            level.NeedsRebuilding.Value = false;
        }

        private void ProcessPathRequests(PathableGraph graph, ref SystemState state) {

            // Cache the paths
            if (PathsJob.Tasks.IsCreated) {
                for (int i = 0; i < PathsJob.Tasks.Length; i++) {
                    var task = PathsJob.Tasks[i];
                    PathCache.Cache[task.CacheKey] = new CachedPortalPath {
                        IsPending = false,
                        HasBeenQueued = false,
                        NoPathExists = !task.Success,
                        Nodes = task.Path
                    };
                }
                PathsJob.Tasks.Dispose();
            }

            var numRequests = PathRequests.Length;
            if (numRequests == 0) {
                return;
            }

            // Allocate the tasks
            var tasks = new NativeList<FindPathsJob.Task>(numRequests, Allocator.TempJob);
            for (int i = 0; i < numRequests; i++) {
                var request = PathRequests[i];

                // Discard duplicate requests
                if (PathCache.Cache.TryGetValue(request.cacheKey, out var existing) && existing.HasBeenQueued) {
                    continue;
                }

                // Prepare the task
                var task = new FindPathsJob.Task {
                    CacheKey = request.cacheKey,
                    Start = request.originCell,
                    Dest = request.destCell,
                    TravelType = request.travelType,
                    Path = new UnsafeList<PortalPathNode>(Constants.EXPECTED_MAX_PATH_LENGTH, Allocator.Persistent),
                    Success = false
                };
                tasks.Add(task);

                // Update the cache
                PathCache.Cache[request.cacheKey] = new CachedPortalPath {
                    IsPending = true,
                    HasBeenQueued = true,
                };

            }
            PathRequests.Clear();

            // Schedule the tasks
            PathsJob = new FindPathsJob(graph, tasks.AsArray());
            state.Dependency = PathsJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

        private void ProcessFlowRequests(PathableGraph graph, ref SystemState state) {

            // Cache the flows
            if (FlowsJob.Tasks.IsCreated) {
                for (int i = 0; i < FlowsJob.Tasks.Length; i++) {
                    var task = FlowsJob.Tasks[i];
                    var result = task.ResultAsFlowField();
                    FlowCache.StoreField (result.SectorIndex, task.CacheKey, new CachedFlowField {
                        IsPending = false,
                        HasBeenQueued = false,
                        FlowField = result,
                    });
                }
                FlowsJob.Tasks.Dispose();
            }

            var numRequests = FlowRequests.Length;
            if (numRequests == 0) {
                return;
            }

            // Allocate the tasks
            var tasks = new NativeList<FindFlowsJob.Task>(numRequests, Allocator.TempJob);
            for (int i = 0; i < numRequests; i++) {
                var request = FlowRequests[i];

                // Discard duplicate requests
                if (FlowCache.TryGetField(request.cacheKey, out var existing) && existing.HasBeenQueued) {
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

                // Prepare the task
                var task = new FindFlowsJob.Task {
                    CacheKey = request.cacheKey,
                    Sector = goalMap,
                    GoalBounds = goalBounds,
                    ExitDirection = request.goalDirection,
                    Flow = new Utils.UnsafeField<float2>(goalMap.Bounds.SizeCells, Allocator.Persistent),
                    Color = 0,
                };
                tasks.Add(task);

                FlowCache.StoreField (goalMap.Index, request.cacheKey, new CachedFlowField {
                    IsPending = true,
                    HasBeenQueued = true,
                });
            }
            FlowRequests.Clear();

            // Schedule the tasks
            FlowsJob = new FindFlowsJob(tasks.AsArray());
            state.Dependency = FlowsJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

    }

}