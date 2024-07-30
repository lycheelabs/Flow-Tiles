using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {
    public struct LevelSetup : IComponentData {

        public int Size;
        public NativeArray<bool> Walls;
        public NativeArray<float4> Colors;
        public NativeArray<float2> Flows;

    }

}