using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestPathsJob : IJobEntity {

        public NativeParallelHashMap<int4, CachedPortalPath> PathCache;
        public NativeList<PathRequest> PathRequests;

        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingPathData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (!PathCache.ContainsKey(key)) {

                // Request a path be generated
                PathRequests.Add(new PathRequest {
                    originCell = data.ValueRO.Start,
                    destCell = data.ValueRO.Dest,
                    cacheKey = key,
                });

                // Store temp data in the cache
                PathCache[key] = new CachedPortalPath {
                    IsPending = true
                };

            }

            // Remove component
            ECB.RemoveComponent<MissingPathData>(entity);
        }
    }

}

