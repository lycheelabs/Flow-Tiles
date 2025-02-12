using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct MissingSightlineData : IComponentData {

        public int2 Start;
        public int2 End;
        public int2 LevelSize;
        public int TravelType;

        public int4 Key => CacheKeys.ToPathKey(Start, End, LevelSize, TravelType);

    }
}