using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestFlowsJob : IJobEntity {

        public NativeParallelHashMap<int4, CachedFlowField> FlowCache;
        public NativeList<FlowRequest> FlowRequests;

        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingFlowData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (!FlowCache.ContainsKey(key)) {

                // Request a flow be generated
                FlowRequests.Add(new FlowRequest {
                    goalCell = data.ValueRO.Cell,
                    goalDirection = data.ValueRO.Direction,
                    travelType = data.ValueRO.TravelType,
                    cacheKey = data.ValueRO.Key,
                });

                // Store temp data in the cache
                FlowCache[key] = new CachedFlowField {
                    IsPending = true
                };

            }

            // Remove component
            ECB.RemoveComponent<MissingFlowData>(entity);
        }
    }

}

