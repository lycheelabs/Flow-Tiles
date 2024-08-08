using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct RebuildGraphJob : IJobFor {

        [ReadOnly] public NativeList<int> Requests;
        public PathableGraph Graph;

        [BurstCompile]
        public void Execute(int index) {
            var sectorIndex = Requests[index];
            Graph.BuildSector(sectorIndex);
        }

    }

}