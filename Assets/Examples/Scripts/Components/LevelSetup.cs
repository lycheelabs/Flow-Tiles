using FlowTiles.Utils;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {
    public struct LevelSetup : IComponentData {

        public int Size;
        public NativeField<bool> Walls;
        public NativeField<float4> Colors;
        public NativeField<float2> Flows;

    }
}