using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct MissingFlowData : IComponentData {

        public int SectorIndex;
        public int4 Key;

        public int2 Cell;
        public int2 Direction;
        public int TravelType;

    }

}