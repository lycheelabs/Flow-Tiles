
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

        private PathCache PathCache;
        private FlowCache FlowCache;

        private NativeList<int> RebuildRequests;
        private NativeQueue<PathRequest> PathRequests;
        private NativeQueue<FlowRequest> FlowRequests;

        private NativeList<FindPathsJob.Task> TempPathTasks;
        private NativeList<FindFlowsJob.Task> TempFlowTasks;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the caches
            PathCache = new PathCache(Constants.MAX_CACHED_PATHS);
            FlowCache = new FlowCache(1000);

            // Build the request buffers
            RebuildRequests = new NativeList<int>(50, Allocator.Persistent);
            PathRequests = new NativeQueue<PathRequest>(Allocator.Persistent);
            FlowRequests = new NativeQueue<FlowRequest>(Allocator.Persistent);

        }

        public void OnDestroy(ref SystemState state) {
            PathCache.Dispose();
            FlowCache.Dispose();

            RebuildRequests.Dispose();
            PathRequests.Dispose(); 
            FlowRequests.Dispose();

            if (TempPathTasks.IsCreated) {
                for (int i = 0; i < TempPathTasks.Length; i++) {
                    TempPathTasks[i].Dispose();
                }
                TempPathTasks.Dispose();
            }

            if (TempFlowTasks.IsCreated) {
                for (int i = 0; i < TempFlowTasks.Length; i++) {
                    TempFlowTasks[i].Dispose();
                }
                TempFlowTasks.Dispose();
            }

        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {

            var ecbEarly = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecbLate = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            if (!globalData.IsInitialised) {
                return;
            }
            
            var level = globalData.Level;
            var graph = globalData.Graph;
            
            // Rebuild all dirty graph sectors
            if (level.NeedsRebuilding.Value) {
                RebuildDirtySectors(ref level, ref graph, ref state);
                level.IsInitialised.Value = true;
                return; // Don't process paths this frame
            }

            // Process path and flow requests, and cache the results
            ProcessPathRequests(graph, ref state);
            ProcessFlowRequests(graph, ref state);

            // Invalidate old paths
            new InvalidatePathsJob {
                PathCache = PathCache,
                ECB = ecbEarly.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate path rquests
            new RequestPathsJob {
                PathCache = PathCache,
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
                PathCache = PathCache,
                FlowCache = FlowCache,
                ECB = ecbLate.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            // Expose flow data for visualisation
            new DebugPathsJob {
                FlowCache = FlowCache,
            }.ScheduleParallel();

        }

        private void RebuildDirtySectors(ref PathableLevel level, ref PathableGraph graph, ref SystemState state) {
            var workRemains = false;

            // Prepare sectors for building
            RebuildRequests.Clear();
            for (int index = 0; index < graph.Layout.NumSectorsInLevel; index++) {
                var flags = level.RebuildFlags[index];
                if (flags.NeedsRebuilding) {

                    // Prepare this sector (once)
                    if (!flags.IsReinitialised) {
                        FlowCache.ClearSector(index);
                        graph.ReinitialiseSector(index, level);
                        flags.IsReinitialised = true;
                    }

                    // Queue this sector (if enough space this frame)
                    if (RebuildRequests.Length < Constants.MAX_FLOWFIELDS_PER_FRAME) {
                        RebuildRequests.Add(index);
                        flags.NeedsRebuilding = false;
                    } else {
                        workRemains = true;
                    }

                    level.RebuildFlags[index] = flags;
                }
            }

            // Once all sectors are built, increase the graph version
            if (!workRemains) {
                level.NeedsRebuilding.Value = false;
                graph.GraphVersion.Value++;
            }

            // Calculate exit points
            // (requires checking neighbors, therefore sectors must be fully reinitialised)
            for (int request = 0; request < RebuildRequests.Length; request++) {
                var index = RebuildRequests[request];
                graph.BuildSectorExits(index);
            }

            // Build internal sector data in parallel
            var job = new RebuildGraphJob {
                Requests = RebuildRequests,
                Graph = graph,
            };
            state.Dependency = job.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);

        }

        private void ProcessPathRequests(PathableGraph graph, ref SystemState state) {

            // Cache the paths
            if (TempPathTasks.IsCreated) {
                for (int i = 0; i < TempPathTasks.Length; i++) {
                    var task = TempPathTasks[i];
                    PathCache.StorePath(task.CacheKey, new CachedPortalPath {
                        IsPending = false,
                        HasBeenQueued = false,
                        NoPathExists = !task.Success,
                        GraphVersionAtSearch = graph.GraphVersion.Value,
                        Nodes = task.Path
                    });
                }
                TempPathTasks.Dispose();
            }

            var numRequests = PathRequests.Count;
            if (numRequests == 0) {
                return;
            }

            // Allocate the tasks
            var numTasks = math.min(numRequests, Constants.MAX_PATHFINDS_PER_FRAME);
            var tasks = new NativeList<FindPathsJob.Task>(numTasks, Allocator.TempJob);
            for (int i = 0; i < numTasks; i++) {
                var request = PathRequests.Dequeue();

                // Discard duplicate requests
                if (PathCache.TryGetPath(request.cacheKey, out var existing) && existing.HasBeenQueued) {
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
                PathCache.StorePath(request.cacheKey, new CachedPortalPath {
                    IsPending = true,
                    HasBeenQueued = true,
                });

            }

            // Schedule the tasks
            TempPathTasks = tasks;
            var pathJob = new FindPathsJob(graph, tasks.AsArray());
            state.Dependency = pathJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

        private void ProcessFlowRequests(PathableGraph graph, ref SystemState state) {

            // Cache the flows
            if (TempFlowTasks.IsCreated) {
                for (int i = 0; i < TempFlowTasks.Length; i++) {
                    var task = TempFlowTasks[i];
                    var result = task.ResultAsFlowField();
                    FlowCache.StoreField (result.SectorIndex, task.CacheKey, new CachedFlowField {
                        IsPending = false,
                        HasBeenQueued = false,
                        FlowField = result,
                    });
                }
                TempFlowTasks.Dispose();
            }

            var numRequests = FlowRequests.Count;
            if (numRequests == 0) {
                return;
            }

            // Allocate the tasks
            var numTasks = math.min(numRequests, Constants.MAX_FLOWFIELDS_PER_FRAME);
            var tasks = new NativeList<FindFlowsJob.Task>(numTasks, Allocator.TempJob);
            for (int i = 0; i < numRequests; i++) {
                var request = FlowRequests.Dequeue();

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

            // Schedule the tasks
            TempFlowTasks = tasks;
            var flowJob = new FindFlowsJob(tasks.AsArray());
            state.Dependency = flowJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

    }

}