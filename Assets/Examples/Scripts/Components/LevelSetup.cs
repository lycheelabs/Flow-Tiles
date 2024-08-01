using FlowTiles.Utils;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {
    public struct LevelSetup : IComponentData {

        public int Size;
        public UnsafeField<bool> Walls;
        public UnsafeField<float4> Colors;
        public UnsafeField<float2> Flows;

    }
}