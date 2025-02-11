using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestFlowsJob : IJobEntity {

        public FlowCache FlowCache;
        public NativeQueue<FlowRequest> FlowRequests;

        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingFlowData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (!FlowCache.ContainsField(key)) {

                // Request a flow be generated
                FlowRequests.Enqueue(new FlowRequest {
                    goalCell = data.ValueRO.Cell,
                    goalDirection = data.ValueRO.Direction,
                    travelType = data.ValueRO.TravelType,
                });

                // Store temp data in the cache
                var sector = data.ValueRO.SectorIndex;
                FlowCache.StoreField (sector, key, new CachedFlowField {
                    IsPending = true
                });

            }

            // Remove component
            ECB.RemoveComponent<MissingFlowData>(entity);
        }
    }

}

