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
                PortalGraph = portalGraph,
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
            public PortalGraphs.PortalGraph PortalGraph;

            [BurstCompile]
            private void Execute(PathfindingData agent, [ChunkIndexInQuery] int sortKey) {
                //var color = PortalGraph.GetColor(agent.OriginCell.x, agent.OriginCell.y);
                var request = new int4(agent.OriginCell, agent.DestCell);
                var cacheHit = PathCache.ContainsKey(request);

                if (!cacheHit) {
                    Requests.Add(new PathRequest {
                        originCell = agent.OriginCell,
                        destCell = agent.DestCell,
                    });
                }
                else {
                    var nodes = PathCache[request].Path;
                    //UnityEngine.Debug.Log("Retrieved path: " + nodes.Length);
                }
            }
        }

    }

}