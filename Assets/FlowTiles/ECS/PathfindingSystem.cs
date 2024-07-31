using NUnit.Framework.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace FlowTiles {

    public partial struct PathfindingSystem : ISystem {

        private Entity CacheSingleton;
        private PathCache Cache;

        //private Entity BufferSingleton;
        private NativeList<PathRequest> Requests;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the cache
            Cache = new PathCache {
                Cache = new NativeParallelHashMap<int4, CachedPath>(1000, Allocator.Persistent) 
            };
            CacheSingleton = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<PathCache>(CacheSingleton);
            state.EntityManager.SetComponentData(CacheSingleton, Cache);

            // Build the request buffers
            Requests = new NativeList<PathRequest>(1000, Allocator.Persistent);

        }

        public void OnUpdate(ref SystemState state) {

            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var portalGraph = globalData.Graph;

            // Process queued requests, and cache the paths
            foreach (var request in Requests) {
                var path = PortalPathfinder.FindPortalPath(portalGraph, request.originCell, request.destCell);
                Cache.Cache[request.cacheKey] = new CachedPath {
                    Path = path
                };
            }
            Requests.Clear();

            // Search for entity paths
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            new RequestPathsJob {
                Cache = Cache.Cache,
                Requests = Requests,
                ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.Schedule();

        }

        public void OnDestroy (ref SystemState state) {
            state.EntityManager.DestroyEntity(CacheSingleton);
            Cache.Cache.Dispose();
            Requests.Dispose();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public NativeList<PathRequest> Requests;
            public EntityCommandBuffer.ParallelWriter ECB;
            public NativeParallelHashMap<int4, CachedPath> Cache;

            [BurstCompile]
            private void Execute(PathfindingData agent, [ChunkIndexInQuery] int sortKey) {
                var request = new int4(agent.OriginCell, agent.DestCell);
                var cacheHit = Cache.ContainsKey(request);

                if (!cacheHit) {
                    Requests.Add(new PathRequest {
                        originCell = agent.OriginCell,
                        destCell = agent.DestCell,
                    });
                }
                else {
                    var nodes = Cache[request].Path;
                    UnityEngine.Debug.Log("Retrieved path: " + nodes.Length);
                }
            }
        }

    }

}