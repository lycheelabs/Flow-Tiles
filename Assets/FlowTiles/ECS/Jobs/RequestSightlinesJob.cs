using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestSightlinesJob : IJobEntity {

        public LineCache LineCache;
        public NativeQueue<LineRequest> LineRequests;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingSightlineData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (!LineCache.ContainsLine(key)) {

                // Request a sightline be generated
                LineRequests.Enqueue(new LineRequest {
                    startCell = data.ValueRO.Start,
                    endCell = data.ValueRO.End,
                    levelSize = data.ValueRO.LevelSize,
                    travelType = data.ValueRO.TravelType,
                });

                // Store temp data in the cache
                LineCache.SetSightline(key, new CachedSightline {
                    IsPending = true
                });

            }

            // Remove component
            ECB.RemoveComponent<MissingSightlineData>(entity);
        }
    }

}

