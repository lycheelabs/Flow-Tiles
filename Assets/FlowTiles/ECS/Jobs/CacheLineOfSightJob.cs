using Unity.Burst;
using Unity.Entities;

namespace FlowTiles.ECS {
    [BurstCompile]
    public partial struct CacheLineOfSightJob : IJobEntity {

        public LineCache LineCache;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<LineOfSightResult> data, Entity entity) {
            var key = data.ValueRO.PathKey;
            LineCache.SetLineOfSight(key, data.ValueRO.LineExists);

            // Remove component
            ECB.RemoveComponent<LineOfSightResult>(entity);
        }
    }

}

