using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {

    public partial struct PathfindingSystem : ISystem {

        private Entity CacheSingleton;
        private PathCache Cache;

        private Entity BufferSingleton;
        private DynamicBuffer<PathRequest> Requests;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<PathRequest>();

            // Build the cache
            Cache = new PathCache {
                Cache = new NativeParallelHashMap<int4, PortalPath>(1000, Allocator.Persistent) 
            };
            CacheSingleton = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<PathCache>(CacheSingleton);
            state.EntityManager.SetComponentData(CacheSingleton, Cache);

            // Build the buffer
            BufferSingleton = state.EntityManager.CreateEntity();
            Requests = state.EntityManager.AddBuffer<PathRequest>(BufferSingleton);
        }

        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            new RequestPathsJob {
               Cache = Cache.Cache,
               BufferSingleton = BufferSingleton,
               ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();

            new BuildPathsJob {
                Cache = Cache.Cache,
            }.Schedule();
        }

        public void OnDestroy (ref SystemState state) {
            state.EntityManager.DestroyEntity(BufferSingleton);
            state.EntityManager.DestroyEntity(CacheSingleton);
            Cache.Cache.Dispose();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public Entity BufferSingleton;
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public NativeParallelHashMap<int4, PortalPath> Cache;

            [BurstCompile]
            private void Execute(PathfindingData agent, [ChunkIndexInQuery] int sortKey) {
                var request = new int4(agent.OriginCell, agent.DestCell);
                var cacheHit = Cache.ContainsKey(request);

                if (!cacheHit) {
                    ECB.AppendToBuffer(sortKey, BufferSingleton, new PathRequest {
                        originCell = agent.OriginCell,
                        destCell = agent.DestCell,
                    });
                }
            }
        }

        [BurstCompile]
        public partial struct BuildPathsJob : IJobEntity {

            public NativeParallelHashMap<int4, PortalPath> Cache;

            [BurstCompile]
            private void Execute(DynamicBuffer<PathRequest> requests, [ChunkIndexInQuery] int sortKey) {
                foreach (var request in requests) {
                    UnityEngine.Debug.Log("REQUEST: " + request.destCell);
                }
                requests.Clear();
            }
        }

    }

}