using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct MissingPathData : IComponentData {

        public int4 Key;
        public int2 Start;
        public int2 Dest;

    }

}