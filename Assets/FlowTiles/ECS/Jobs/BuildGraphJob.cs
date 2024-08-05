using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct BuildGraphJob : IJobFor {

        [ReadOnly] public NativeList<int> Requests;
        public NativeArray<CostSector> Costs;
        public NativeArray<PortalSector> Portals;

        [BurstCompile]
        public void Execute(int index) {
            var sectorIndex = Requests[index];
            var costSector = Costs[sectorIndex];

            var portalSector = Portals[sectorIndex];
            var size = costSector.Bounds.SizeCells;
            var numCells = size.x * size.y;
            SectorPathfinder pathfinder = new SectorPathfinder(numCells, Allocator.Temp);
            
            costSector.CalculateColors();
            portalSector.BuildInternalConnections(costSector, pathfinder);

            Costs[index] = costSector;
            Portals[index] = portalSector;        
        }

    }

}