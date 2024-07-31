using System;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {
    public struct FlowFieldTile {

        public int2 Size;
        public NativeArray<double2> Directions;
        public NativeArray<double> Gradients;
        public TimeSpan GenerationTime;

        public void Dispose() {
            Directions.Dispose();
            Gradients.Dispose();
        }

        public float2 GetFlow (int x, int y) {
            var index = (x + 1) + (y + 1) * (Size.x + 2);
            return (float2)Directions[index];
        }

    }

}