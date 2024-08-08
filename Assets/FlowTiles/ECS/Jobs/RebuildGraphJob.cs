using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct RebuildGraphJob : IJobFor {

        [ReadOnly] public NativeList<int> Requests;

        [NativeDisableParallelForRestriction]
        // This job is still safe, because the indexes in 'Requests' are used instead of the job indexes,
        // And these indexes are always unique.
        public PathableGraph Graph;

        [BurstCompile]
        public void Execute(int index) {
            var sectorIndex = Requests[index];
            Graph.BuildSector(sectorIndex);
        }

    }

}