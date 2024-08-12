using Unity.Burst;
using Unity.Entities;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct InvalidatePathsJob : IJobEntity {

        public PathCache PathCache;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<InvalidPathData> data, Entity entity) {

            PathCache.DisposePath(data.ValueRO.Key);

            // Remove component
            ECB.RemoveComponent<InvalidPathData>(entity);
        }
    }

}

