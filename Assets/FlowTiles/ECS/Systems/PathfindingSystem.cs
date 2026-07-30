using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.ECS {

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct PathfindingSystem : ISystem {

        public static void SetLevel (PathableGrid gridData, PathableGraph graphData) {
            var world = World.DefaultGameObjectInjectionWorld;
            var handle = world.Unmanaged.GetExistingUnmanagedSystem<PathfindingSystem>();
            ref var sys = ref world.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle);

            sys.LevelIsInitialised = true;
            sys.Level = gridData;
            sys.Graph = graphData;
        }

        public static void ClearLevel () {
            ThreadSafety.EnsureECSThreadSafety();

            var world = World.DefaultGameObjectInjectionWorld;
            var handle = world.Unmanaged.GetExistingUnmanagedSystem<PathfindingSystem>();
            ref var sys = ref world.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle);

            sys.LastScheduledHandle.Complete();
            sys.DisposeInFlightJobs();

            // Dispose all cached flow/path data
            sys.FlowCache.Clear();
            sys.PathCache.Clear();

            // Clear pending requests so nothing references old level state
            if (sys.RebuildRequests.IsCreated) sys.RebuildRequests.Clear();
            if (sys.PathRequests.IsCreated) while (sys.PathRequests.TryDequeue(out _)) { }
            if (sys.FlowRequests.IsCreated) while (sys.FlowRequests.TryDequeue(out _)) { }
            if (sys.LineRequests.IsCreated) while (sys.LineRequests.TryDequeue(out _)) { }
            if (sys.PathInFlightKeys.IsCreated) sys.PathInFlightKeys.Clear();

            // Clear static references. These data structures are disposed elsewhere
            sys.LevelIsInitialised = false;
            sys.Level = default;
            sys.Graph = default;
        }

        // --------------------------------------------------------------

        // Level
        private bool LevelIsInitialised;
        private PathableGrid Level;
        private PathableGraph Graph;

        // Pathfinders
        private ContinentPathfinder ContinentPathfinder;

        // Caches
        private PathCache PathCache;
        private FlowCache FlowCache;
        private LineCache LineCache;

        // Requests
        private NativeList<GraphSector> RebuildRequests;
        private NativeQueue<PathRequest> PathRequests;
        private NativeQueue<FlowRequest> FlowRequests;
        private NativeQueue<LineRequest> LineRequests;

        // Tracks path keys that have been enqueued but not yet promoted to a FindPathsJob task.
        // Used to dedup re-emissions from FollowPathsJob while a request is still queued.
        // PathCache cannot be used as the placeholder (it is bounded; placeholders would
        // either deadlock WaitForCapacity or thrash the cache).
        private NativeHashSet<int4> PathInFlightKeys;

        // Tasks
        private bool IsRebuilding;
        private RebuildGraphJob RebuildTask;
        private NativeList<FindPathsJob.Task> TempPathTasks;
        private NativeList<FindFlowsJob.Task> TempFlowTasks;
        private NativeList<FindSightlinesJob.Task> TempLineTasks;

        // The combined job handle at the end of the last OnUpdate, stored so that
        // ClearLevel (a static method with no SystemState) can complete them before
        // disposing in-flight task data.
        private JobHandle LastScheduledHandle;

        public void OnCreate(ref SystemState state) {
            ContinentPathfinder = new ContinentPathfinder(Allocator.Persistent);
            
            // Build the caches
            PathCache = new PathCache(PathfindingConstants.MAX_CACHED_PATHS);
            FlowCache = new FlowCache(PathfindingConstants.EXPECTED_SECTORS_IN_MAP * 10);
            LineCache = new LineCache(PathfindingConstants.MAX_CACHED_PATHS);

            // Build the request buffers
            RebuildRequests = new NativeList<GraphSector>(50, Allocator.Persistent);
            PathRequests = new NativeQueue<PathRequest>(Allocator.Persistent);
            FlowRequests = new NativeQueue<FlowRequest>(Allocator.Persistent);
            LineRequests = new NativeQueue<LineRequest>(Allocator.Persistent);
            PathInFlightKeys = new NativeHashSet<int4>(256, Allocator.Persistent);

        }

        public void OnDestroy(ref SystemState state) {
            ContinentPathfinder.Dispose();

            PathCache.Dispose();
            FlowCache.Dispose();
            LineCache.Dispose();

            if (RebuildRequests.IsCreated) RebuildRequests.Dispose();
            if (PathRequests.IsCreated) PathRequests.Dispose(); 
            if (FlowRequests.IsCreated) FlowRequests.Dispose();
            if (LineRequests.IsCreated) LineRequests.Dispose();
            if (PathInFlightKeys.IsCreated) PathInFlightKeys.Dispose();

            DisposeInFlightJobs();
        }

        // Dispose calculation jobs that have not yet finished. Also dispose their results because they will not be cached.
        private void DisposeInFlightJobs () {
            if (TempPathTasks.IsCreated) {
                for (int i = 0; i < TempPathTasks.Length; i++) {
                    TempPathTasks[i].DisposeAll();
                }
                TempPathTasks.Dispose();
            }

            if (TempFlowTasks.IsCreated) {
                for (int i = 0; i < TempFlowTasks.Length; i++) {
                    TempFlowTasks[i].DisposeAll();
                }
                TempFlowTasks.Dispose();
            }

            if (TempLineTasks.IsCreated) {
                for (int i = 0; i < TempLineTasks.Length; i++) {
                    TempLineTasks[i].DisposeAll();
                }
                TempLineTasks.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {

            // Check pathfinding has been initialised
            if (!LevelIsInitialised) {
                return;
            }

            // Complete all pathfinding jobs from last frame to avoid race conditions during state update
            state.Dependency.Complete();

            // Cache all data calculated in parallel (from last frame)
            CacheCalculationsFromLastFrame(Graph.GraphVersion.Value);

            // Rebuild all dirty graph sectors (spread across multiple frames)
            if (RebuildGraph (ref state)) {
                // Pause pathfinding until the rebuild is complete
                return;
            }

            // First-time build is complete. Pathing can begin!
            Level.IsInitialised.Value = true;

            // Calculate queued path, flow and sightline requests
            ProcessPathRequests(ref state);
            ProcessFlowRequests(ref state);
            ProcessLineRequests(ref state);

            // Check agents for any data-requesting components (added last frame)
            FindNewRequests(ref state);

            // Each agent attempts to follow its path, and adds request components as needed
            FollowPaths(ref state);

            // Capture the final handle so ClearLevel can safely wait for these jobs.
            LastScheduledHandle = state.Dependency;
        }

        private bool RebuildGraph(ref SystemState state) {
            var modified = false;
            if (IsRebuilding) {
                CompleteSectorRebuild(ref state);
                modified = true;
            }
            if (Level.NeedsRebuilding.Value) {
                ScheduleSectorRebuild(ref state);
                modified = true;
            }
            return modified;
        }

        private void ScheduleSectorRebuild(ref SystemState state) {    
            var workRemains = false;

            // Prepare sectors for building
            RebuildRequests.Clear();
            for (int index = 0; index < Graph.Layout.NumSectorsInLevel; index++) {
                var flags = Level.RebuildFlags[index];
                if (flags.NeedsRebuilding) {

                    // Queue this sector (if enough space this frame)
                    if (RebuildRequests.Length < PathfindingConstants.MAX_REBUILDS_PER_FRAME) {
                        FlowCache.ClearSector(index);
                        var newSector = Graph.InstantiateSector(index, Level);
                        RebuildRequests.Add(newSector);
                        flags.NeedsRebuilding = false;
                    } 
                    // Else, hold for next frame
                    else {
                        workRemains = true;
                    }

                    Level.RebuildFlags[index] = flags;
                }
            }
            // Once all sectors are built, increase the graph version
            if (!workRemains) {
                Level.NeedsRebuilding.Value = false;
                Graph.GraphVersion.Value++;
            }
            if (RebuildRequests.Length == 0) {
                return;
            }

            // Build internal sector data in parallel
            IsRebuilding = true;
            RebuildTask = new RebuildGraphJob {
                Requests = RebuildRequests.AsArray(),
            };
            state.Dependency = RebuildTask.ScheduleParallel(RebuildRequests.Length, 1, state.Dependency);
        }

        private void CompleteSectorRebuild(ref SystemState state) {
            for (int i = 0; i < RebuildRequests.Length; i++) {
                var result = RebuildRequests[i];
                var index = result.Index;
                Graph.StoreSector(index, result);
            }
            RebuildTask = default;
            IsRebuilding = false;

            // Once all sectors are built, recalculate the graph continents
            if (!Level.NeedsRebuilding.Value) {
                var continentsJob = new RecalculateContinentsJob(Graph, ContinentPathfinder);
                state.Dependency = continentsJob.Schedule(state.Dependency);
            }
        }

        private void CacheCalculationsFromLastFrame (int graphVersion) {

            // Cache new paths
            if (TempPathTasks.IsCreated) {
                for (int i = 0; i < TempPathTasks.Length; i++) {
                    var task = TempPathTasks[i];
                    PathCache.StorePath(task.CacheKey, new CachedPortalPath {
                        StartCell = task.Start,
                        Nodes = task.Path,
                        GraphVersionAtSearch = graphVersion,
                        PathWasFound = task.Success[0],
                        // No longer pending
                    });
                    task.Dispose();
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
                    task.Dispose();
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
                    task.Dispose();
                }
                TempLineTasks.Dispose();
            }

        }

        private void ProcessPathRequests(ref SystemState state) {

            var numRequests = PathRequests.Count;
            if (numRequests == 0) {
                return;
            }

            var now = (float)SystemAPI.Time.ElapsedTime;

            // Cap how many tasks we schedule this frame.
            // Snapshot the queue count so re-enqueued requests are not re-examined this frame.
            var numTasks = math.min(numRequests, PathfindingConstants.MAX_PATHFINDS_PER_FRAME);
            var tasks = new NativeList<FindPathsJob.Task>(numTasks, Allocator.TempJob);
            var snapshot = numRequests;

            while (tasks.Length < numTasks && snapshot > 0 && PathRequests.Count > 0) {
                snapshot--;
                var request = PathRequests.Dequeue();

                // If oldest path is still pending, wait for it to complete before accepting new requests.
                // Defer this request: put it back so it survives until next frame.
                if (PathCache.WaitForCapacity(now, pendingTimeoutSeconds: 3f)) {
                    PathRequests.Enqueue(request);
                    break;
                }

                // Discard duplicate requests (already promoted to a real task by an earlier frame)
                if (PathCache.TryGetPath(request.CacheKey, out var existing) && existing.HasBeenQueued) {
                    PathInFlightKeys.Remove(request.CacheKey);
                    continue;
                }

                // Request flow fields for start and dest cells
                CachedFlowField startField;
                CachedFlowField destField;
                var startFieldKey = CacheKeys.ToFlowKey(request.originCell, 0, request.travelType);
                var destFieldKey = CacheKeys.ToFlowKey(request.destCell, 0, request.travelType);
                var flowCacheMiss = false;

                if (!FlowCache.TryGetField(startFieldKey, out startField)) {
                    flowCacheMiss = true;

                    // Request a start field
                    FlowRequests.Enqueue(new FlowRequest {
                        goalCell = request.originCell,
                        travelType = request.travelType,
                    });
                } else {
                    flowCacheMiss |= startField.IsPending;
                }

                if (!FlowCache.TryGetField(destFieldKey, out destField)) {
                    flowCacheMiss = true;

                    // Request a dest field
                    FlowRequests.Enqueue(new FlowRequest {
                        goalCell = request.destCell,
                        travelType = request.travelType,
                    });
                } else {
                    flowCacheMiss |= destField.IsPending;
                }

                // Defer this request until the flow tiles are ready.
                // Keep the in-flight key marker so agents do not re-emit while we wait.
                if (flowCacheMiss) {
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
                    Path = new UnsafeList<PortalPathNode>(PathfindingConstants.EXPECTED_MAX_PATH_LENGTH, Allocator.Persistent),
                    Success = new UnsafeArray<bool>(1, Allocator.TempJob),
                };
                tasks.Add(task);

                // Promote: the request is now a real pending task, drop the in-flight marker.
                PathInFlightKeys.Remove(request.CacheKey);

                // Update the cache
                PathCache.StorePath(request.CacheKey, new CachedPortalPath {
                    StartCell = request.originCell,
                    HasBeenQueued = true,
                    IsPending = true,
                    PendingSinceTime = now,
                });
            }

            // No PathRequests.Clear() - any unprocessed requests stay in the queue for next frame.

            // Schedule the tasks
            TempPathTasks = tasks;
            var pathJob = new FindPathsJob(Graph, tasks.AsArray());
            state.Dependency = pathJob.ScheduleParallel(tasks.Length, 1, state.Dependency);
        }

        private void ProcessFlowRequests(ref SystemState state) {

            var numRequests = FlowRequests.Count;
            if (numRequests == 0) {
                return;
            }

            // Cap how many tasks we schedule this frame.
            // Snapshot the queue count so re-enqueued requests are not re-examined this frame.
            var numTasks = math.min(numRequests, PathfindingConstants.MAX_FLOWFIELDS_PER_FRAME);
            var tasks = new NativeList<FindFlowsJob.Task>(numTasks, Allocator.TempJob);
            var snapshot = numRequests;

            while (tasks.Length < numTasks && snapshot > 0 && FlowRequests.Count > 0) {
                snapshot--;
                var request = FlowRequests.Dequeue();

                // Discard duplicate requests
                if (FlowCache.TryGetField(request.CacheKey, out var existing) && existing.HasBeenQueued) {
                    continue;
                }

                // Find the goal boundaries
                var goal = request.goalCell;
                var travelType = request.travelType;
                var goalMap = Graph.CellToSectorMap(goal, travelType);
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
                    Flow = new UnsafeField<float2>(sizeCells, Allocator.Persistent),
                    Distances = new UnsafeField<int>(sizeCells, Allocator.Persistent),
                };
                tasks.Add(task);

                FlowCache.StoreField (goalMap.Index, request.CacheKey, new CachedFlowField {
                    IsPending = true,
                    HasBeenQueued = true,
                });
            }

            // No FlowRequests.Clear() - any unprocessed requests stay in the queue for next frame.

            // Schedule the tasks
            TempFlowTasks = tasks;
            var flowJob = new FindFlowsJob(tasks.AsArray());
            state.Dependency = flowJob.ScheduleParallel(tasks.Length, 1, state.Dependency);

        }

        private void ProcessLineRequests(ref SystemState state) {

            var numRequests = LineRequests.Count;
            if (numRequests == 0) {
                return;
            }

            // Cap how many tasks we schedule this frame.
            // Snapshot the queue count so re-enqueued requests are not re-examined this frame.
            var numTasks = math.min(numRequests, PathfindingConstants.MAX_SIGHTLINES_PER_FRAME);
            var tasks = new NativeList<FindSightlinesJob.Task>(numTasks, Allocator.TempJob);
            var graphVersion = Graph.GraphVersion.Value;
            var snapshot = numRequests;

            while (tasks.Length < numTasks && snapshot > 0 && LineRequests.Count > 0) {
                snapshot--;
                var request = LineRequests.Dequeue();

                // Discard duplicate requests
                if (LineCache.TryGetSightline(request.CacheKey, graphVersion, out var existing) && existing.HasBeenQueued) {
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
                    GraphVersionAtSearch = graphVersion,
                });
            }

            // No LineRequests.Clear() - any unprocessed requests stay in the queue for next frame.

            // Schedule the tasks
            TempLineTasks = tasks;
            var sightlineJob = new FindSightlinesJob(tasks.AsArray(), Graph);
            state.Dependency = sightlineJob.ScheduleParallel(tasks.Length, 4, state.Dependency);

        }

        // These jobs cannot be multi-threaded
        private void FindNewRequests( ref SystemState state) {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();

            var dependency = state.Dependency;

            // Invalidate old paths
            dependency = new InvalidatePathsJob {
                PathCache = PathCache,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(dependency);

            // Accumulate path requests
            dependency = new RequestPathsJob {
                PathCache = PathCache,
                PathRequests = PathRequests,
                PathInFlightKeys = PathInFlightKeys,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(dependency);

            // Accumulate flow requests
            dependency = new RequestFlowsJob {
                FlowCache = FlowCache,
                FlowRequests = FlowRequests,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }. Schedule(dependency);

            // Accumulate line requests
            dependency = new RequestSightlinesJob {
                LineCache = LineCache,
                LineRequests = LineRequests,
                GraphVersion = Graph.GraphVersion.Value,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(dependency);

            state.Dependency = dependency;
        }

        private void FollowPaths(ref SystemState state) {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();

            var dependency = state.Dependency;

            // Follow the paths
            dependency = new FollowPathsJob {
                Graph = Graph,
                PathCache = PathCache,
                FlowCache = FlowCache,
                LineCache = LineCache,
                PathInFlightKeys = PathInFlightKeys,
                ECB = ecb.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(dependency);

            if (Application.isEditor) {
                // Expose flow data of each agent for debug visualisation (optional)
                dependency = new DebugPathsJob {
                    FlowCache = FlowCache,
                }.ScheduleParallel(dependency);
            }

            state.Dependency = dependency;
        }

        public static NativeArray<CachedPortalPath> FindAllCachedPaths (Allocator allocator = Allocator.Temp) {
            ThreadSafety.EnsureECSThreadSafety();
            var world = World.DefaultGameObjectInjectionWorld;
            var handle = world.Unmanaged.GetExistingUnmanagedSystem<PathfindingSystem>();
            ref var sys = ref world.Unmanaged.GetUnsafeSystemRef<PathfindingSystem>(handle);
            return sys.PathCache.GetAllValues(allocator);
        }

    }

}