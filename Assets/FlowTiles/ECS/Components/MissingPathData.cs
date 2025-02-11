using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct MissingPathData : IComponentData {

        public int2 Start;
        public int2 Dest;
        public int2 LevelSize;
        public int TravelType;

        public int4 Key => PathCache.ToKey(Start, Dest, LevelSize, TravelType);

    }
}