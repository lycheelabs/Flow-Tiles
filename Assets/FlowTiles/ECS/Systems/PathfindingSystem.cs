using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct PathfindingSystem : ISystem {

        private ContinentPathfinder ContinentPathfinder;

        private PathCache PathCache;
        private FlowCache FlowCache;
        private LineCache LineCache;

        private NativeList<int> RebuildRequests;
        private NativeQueue<PathRequest> PathRequests;
        private NativeQueue<FlowRequest> FlowRequests;
        private NativeQueue<LineRequest> LineRequests;

        private NativeList<FindPathsJob.Task> TempPathTasks;
        private NativeList<FindFlowsJob.Task> TempFlowTasks;
        private NativeList<FindSightlinesJob.Task> TempLineTasks;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            ContinentPathfinder = new ContinentPathfinder {
                QueuedNodes = new NativeQueue<Portal>(Allocator.Persistent)
            };
            
            // Build the caches
            PathCache = new PathCache(Constants.MAX_CACHED_PATHS);
            FlowCache = new FlowCache(1000);
            LineCache = new LineCache(1000);

            // Build the request buffers
            RebuildRequests = new NativeList<int>(50, Allocator.Persistent);
            PathRequests = new NativeQueue<PathRequest>(Allocator.Persistent);
            FlowRequests = new NativeQueue<FlowRequest>(Allocator.Persistent);
            LineRequests = new NativeQueue<LineRequest>(Allocator.Persistent);

        }

        public void OnDestroy(ref SystemState state) {
            ContinentPathfinder.Dispose();

            PathCache.Dispose();
            FlowCache.Dispose();
            LineCache.Dispose();

            RebuildRequests.Dispose();
            PathRequests.Dispose(); 
            FlowRequests.Dispose();
            LineRequests.Dispose();

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

            if (TempLineTasks.IsCreated) {
                for (int i = 0; i < TempLineTasks.Length; i++) {
                    TempLineTasks[i].Dispose();
                }
                TempLineTasks.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {

            // Check pathfinding has been initialised
            var data = SystemAPI.GetSingleton<GlobalPathfindingData>();
            if (!data.IsInitialised) {
                return;
            }

            // Cache all data calculated in parallel (from last frame)
            CacheCalculationsFromLastFrame(data.Graph.GraphVersion.Value);

            // Rebuild all dirty graph sectors (spread across multiple frames)
            if (data.Level.NeedsRebuilding.Value) {
                RebuildDirtySectors(data.Level, data.Graph, ref state);
                return;
            }

            // First-time build is complete. Pathing can begin!
            data.Level.IsInitialised.Value = true;

            // Calculate queued path, flow and sightline requests
            ProcessPathRequests(data.Graph, ref state);
            ProcessFlowRequests(data.Graph, ref state);
            ProcessLineRequests(data.Graph, ref state);

            // Check agents for any data-requesting components (added last frame)
            FindNewRequests(data.Graph, ref state);

            // Each agent attempts to follow its path, and adds request components as needed
            FollowPaths(data.Graph, ref state);

        }

        private void RebuildDirtySectors(PathableLevel level, PathableGraph graph, ref SystemState state) {
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
                graph.BuildPortals(index);
            }

            // Build internal sector data in parallel
            var sectorsJob = new RebuildGraphJob {
                Requests = RebuildRequests,
                Graph = graph,
            };
            state.Dependency = sectorsJob.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);

            // Once all sectors are built, recalculate the graph continents
            if (!workRemains) {
                var continentsJob = new RecalculateContinentsJob(graph, ContinentPathfinder);
                state.Dependency = continentsJob.Schedule(state.Dependency);
            }

        }

        private void CacheCalculationsFromLastFrame (int graphVersion) {

            // Cache new paths
            if (TempPathTasks.IsCreated) {
                for (int i = 0; i < TempPathTasks.Length; i++) {
                    var task = TempPathTasks[i];
                    if (task.Success[0]) {
                        PathCache.StorePath(task.CacheKey, new CachedPortalPath {
                            GraphVersionAtSearch = graphVersion,
                            Nodes = task.Path
                        });
                    }
                    task.DisposeTempData();
                }
                TempPathTasks.Dispose();
            }

            // Cache new flows
            if (TempFlowTasks.IsCreated) {
                for (int i = 0; i < TempFlowTasks.Length; i++) {
                    var task = TempFlowTasks[i];
                    var result = task.ResultAsFlowField();
                    FlowCache.StoreField(result.SectorIndex, task.CacheKey, new CachedFlowField {
                        FlowField = result,
                    });
                    task.DisposeTempData();
                }
                TempFlowTasks.Dispose();
            }

            // Cache new lines
            if (TempLineTasks.IsCreated) {
                for (int i = 0; i < TempLineTasks.Length; i++) {
                    var task = TempLineTasks[i];
                    LineCache.SetSightline(task.CacheKey, new CachedSightline {
                        WasFound = task.SightlineExists[0],
                        GraphVersionAtSearch = graphVersion,
                    });
                    task.DisposeTempData();
                }
                TempLineTasks.Dispose();
            }

        }

        private void ProcessPathRequests(PathableGraph graph, ref SystemState state) {

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
                if (PathCache.TryGetPath(request.CacheKey, out var existing) && existing.HasBeenQueued) {
                    continue;
                }

                // Check dest field exists
                CachedFlowField startField;
                CachedFlowField destField;
                var startFieldKey = CacheKeys.ToFlowKey(request.originCell, 0, request.travelType);
                var destFieldKey = CacheKeys.ToFlowKey(request.destCell, 0, request.travelType);
                var failed = false;

                if (!FlowCache.TryGetField(startFieldKey, out startField)) {
                    failed = true;

                    // Request a start field
                    FlowRequests.Enqueue(new FlowRequest {
                        goalCell = request.originCell,
                        travelType = request.travelType,
                    });
                } else {
                    failed |= startField.IsPending;
                }

                if (!FlowCache.TryGetField(destFieldKey, out destField)) {
                    failed = true;

                    // Request a dest field
                    FlowRequests.Enqueue(new FlowRequest {
                        goalCell = request.destCell,
                        travelType = request.travelType,
                    });
                } else {
                    failed |= destField.IsPending;
                }

                if (failed) {
                    PathRequests.Enqueue(request);
                    continue;
                }

                // Prepare the task
                var task = new FindPathsJob.Task {
                    CacheKey = request.CacheKey,
                    Start = request.originCell,
                    Dest = request.destCell,
                    StartField = startField.FlowField,
                    DestField = destField.FlowField,
                    TravelType = request.travelType,
                    Path = new UnsafeList<PortalPathNode>(Constants.EXPECTED_MAX_PATH_LENGTH, Allocator.Persistent),
                    Success = new UnsafeArray<bool>(1, Allocator.TempJob),
                };
                tasks.Add(task);

                // Update the cache
                PathCache.StorePath(request.CacheKey, new CachedPortalPath {
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
                if (FlowCache.TryGetField(request.CacheKey, out var existing) && existing.HasBeenQueued) {
                    continue;
                }

                // Find the goal boundaries
                var goal = request.goalCell;
                var travelType = request.travelType;
                var goalMap = graph.CellToSectorMap(goal, travelType);
                var goalBounds = new CellRect(goal, goal);

                if (!request.goalDirection.Equals(0)) {
                    if (goalMap.TryGetPortal(goal, out var portal)) {
                        goalBounds = portal.Bounds;
                    }
                }

                // Prepare the task
                var sizeCells = goalMap.Bounds.SizeCells;
                var task = new FindFlowsJob.Task {
                    CacheKey = request.CacheKey,
                    Sector = goalMap,
                    GoalBounds = goalBounds,
                    ExitDirection = request.goalDirection,
                    Flow = new Utils.UnsafeField<float2>(sizeCells, Allocator.Persistent),
                    Distances = new Utils.UnsafeField<int>(sizeCells, Allocator.Persistent),
                };
                tasks.Add(task);

                FlowCache.StoreField (goalMap.Index, request.CacheKey, new CachedFlowField {
                    IsPending = true,
                    HasBeenQueued = true,
                });
            }

            // Schedule the tasks
            TempFlowTasks = tasks;
            var flowJob = new FindFlowsJob(tasks.AsArray());
            state.Dependency = flowJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

        private void ProcessLineRequests(PathableGraph graph, ref SystemState state) {

            var numRequests = LineRequests.Count;
            if (numRequests == 0) {
                return;
            }

            // Allocate the tasks
            var numTasks = math.min(numRequests, Constants.MAX_SIGHTLINES_PER_FRAME);
            var tasks = new NativeList<FindSightlinesJob.Task>(numTasks, Allocator.TempJob);
            for (int i = 0; i < numRequests; i++) {
                var request = LineRequests.Dequeue();

                // Discard duplicate requests
                if (LineCache.TryGetSightline(request.CacheKey, out var existing) && existing.HasBeenQueued) {
                    continue;
                }

                // Prepare the task
                var task = new FindSightlinesJob.Task {
                    CacheKey = request.CacheKey,
                    StartCell = request.startCell,
                    EndCell = request.endCell,
                    TravelType = request.travelType,
                    SightlineExists = new UnsafeArray<bool>(1, Allocator.TempJob),
                };
                tasks.Add(task);

                LineCache.SetSightline(request.CacheKey, new CachedSightline {
                    IsPending = true,
                    HasBeenQueued = true,
                    GraphVersionAtSearch = graph.GraphVersion.Value,
                });
            }

            // Schedule the tasks
            TempLineTasks = tasks;
            var sightlineJob = new FindSightlinesJob(tasks.AsArray(), graph);
            state.Dependency = sightlineJob.ScheduleParallel(tasks.Length, 4, state.Dependency);

        }

        // These jobs cannot be multi-threaded
        private void FindNewRequests(PathableGraph graph, ref SystemState state) {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

            // Invalidate old paths
            new InvalidatePathsJob {
                PathCache = PathCache,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate path requests
            new RequestPathsJob {
                PathCache = PathCache,
                PathRequests = PathRequests,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate flow requests
            new RequestFlowsJob {
                FlowCache = FlowCache,
                FlowRequests = FlowRequests,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

            // Accumulate line requests
            new RequestSightlinesJob {
                LineCache = LineCache,
                LineRequests = LineRequests,
                GraphVersion = graph.GraphVersion.Value,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();

        }

        private SystemState FollowPaths(PathableGraph graph, ref SystemState state) {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            // Follow the paths
            new FollowPathsJob {
                Graph = graph,
                PathCache = PathCache,
                FlowCache = FlowCache,
                LineCache = LineCache,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            // Expose flow data of each agent for debug visualisation (optional)
            new DebugPathsJob {
                FlowCache = FlowCache,
            }.ScheduleParallel();

            return state;
        }

    }

}