using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct InvalidPathData : IComponentData {

        public int4 Key;

    }
}