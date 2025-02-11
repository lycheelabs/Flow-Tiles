using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct MissingFlowData : IComponentData {

        public int SectorIndex;

        public int2 Cell;
        public int2 Direction;
        public int TravelType;

        public int4 Key => FlowCache.ToKey(Cell, Direction, TravelType);

    }

}