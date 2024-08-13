using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestPathsJob : IJobEntity {

        public PathCache PathCache;
        public NativeQueue<PathRequest> PathRequests;

        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingPathData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (!PathCache.ContainsPath(key)) {

                // Request a path be generated
                PathRequests.Enqueue(new PathRequest {
                    originCell = data.ValueRO.Start,
                    destCell = data.ValueRO.Dest,
                    travelType = data.ValueRO.TravelType,
                    cacheKey = key,
                });

                // Store temp data in the cache
                PathCache.StorePath(key, new CachedPortalPath {
                    IsPending = true
                });

            }

            // Remove component
            ECB.RemoveComponent<MissingPathData>(entity);
        }
    }

}

