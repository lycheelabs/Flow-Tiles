using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct DebugPathsJob : IJobEntity {

        [ReadOnly] public NativeParallelHashMap<int4, CachedFlowField> FlowCache;

        [BurstCompile]
        private void Execute(RefRO<FlowProgress> progress, ref FlowDebugData debug) {
            debug.CurrentFlowTile = default;

            if (!progress.ValueRO.HasFlow) {
                return;
            }
            var foundFlow = FlowCache.TryGetValue(progress.ValueRO.FlowKey, out var flow);
            if (!foundFlow || flow.IsPending) {
                return;
            }

            debug.CurrentFlowTile = flow.FlowField;
        }
    }

}

