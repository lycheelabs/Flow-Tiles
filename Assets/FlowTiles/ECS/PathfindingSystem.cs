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

        }

        public void OnUpdate(ref SystemState state) {

            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var portalGraph = globalData.Graph;

            // Process pathfinding requests, and cache the results
            foreach (var request in PathRequests) {
                //UnityEngine.Debug.Log("Creating path: " + request.cacheKey);
                var path = PortalPathfinder.FindPortalPath(portalGraph, request.originCell, request.destCell);
                PathCache.Cache[request.cacheKey] = new PortalPath {
                    Path = path
                };
            }
            PathRequests.Clear();

            // TODO: Process flow field requests, and cache the results

            // Search for entity paths
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            new RequestPathsJob {
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                Requests = PathRequests,
                CostMap = portalGraph.Costs,
                ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.Schedule();

        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public NativeList<PathRequest> Requests;
            public EntityCommandBuffer.ParallelWriter ECB;
            public NativeParallelHashMap<int4, PortalPath> PathCache;
            public NativeParallelHashMap<int4, FlowFieldTile> FlowCache;
            public CostMap CostMap;

            [BurstCompile]
            private void Execute(PathfindingData agent, [ChunkIndexInQuery] int sortKey) {
                var origin = agent.OriginCell;
                var originSector = CostMap.GetSectorIndex(origin.x, origin.y);
                var originColor = CostMap.GetColor(origin.x, origin.y);

                var dest = agent.DestCell;
                var destSector = CostMap.GetSectorIndex(dest.x, dest.y);
                var destColor = CostMap.GetColor(dest.x, dest.y);

                var pathKey = new int4(originSector, originColor, destSector, destColor);
                var cacheHit = PathCache.ContainsKey(pathKey);

                if (!cacheHit) {
                    Requests.Add(new PathRequest {
                        originCell = agent.OriginCell,
                        destCell = agent.DestCell,
                        cacheKey = pathKey,
                    });
                }
                else {
                    var nodes = PathCache[pathKey].Path;
                    //UnityEngine.Debug.Log("Retrieved path: " + nodes.Length);
                }
            }
        }

    }

}