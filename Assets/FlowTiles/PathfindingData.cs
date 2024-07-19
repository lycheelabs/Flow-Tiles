using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {
    public struct PathfindingData : IComponentData {

        public int2 OriginCell;
        public int2 DestCell;

    }

}