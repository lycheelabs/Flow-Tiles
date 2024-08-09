using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct InvalidatePathsJob : IJobEntity {

        public NativeParallelHashMap<int4, CachedPortalPath> PathCache;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<InvalidPathData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (PathCache.ContainsKey(key)) {

                // Dispose of the invalid path
                var oldPath = PathCache[key];
                oldPath.Dispose();
                PathCache.Remove(key);

            }

            // Remove component
            ECB.RemoveComponent<InvalidPathData>(entity);
        }
    }

    [BurstCompile]
    public partial struct InvalidateFlowsJob : IJobEntity {

        public NativeParallelHashMap<int4, CachedFlowField> FlowCache;
        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<InvalidFlowData> data, Entity entity) {
            var key = data.ValueRO.Key;
            if (FlowCache.ContainsKey(key)) {

                // Dispose of the invalid path
                var oldFlow = FlowCache[key];
                oldFlow.Dispose();
                FlowCache.Remove(key);

            }

            // Remove component
            ECB.RemoveComponent<InvalidFlowData>(entity);
        }
    }

}

