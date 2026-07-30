using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct RebuildGraphJob : IJobFor {

        public NativeArray<GraphSector> Requests;

        [BurstCompile]
        public void Execute(int index) {
            var sector = Requests[index];
            sector.Calculate();
            Requests[index] = sector;
        }

    }
    
}