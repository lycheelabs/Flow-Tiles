using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct InvalidatePathsJob : IJobEntity {

        public PathCache PathCache;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<InvalidPathData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (PathCache.ContainsPath(key)) {
                PathCache.DisposePath(key);
            }

            // Remove component
            ECB.RemoveComponent<InvalidPathData>(entity);
        }
    }

}

