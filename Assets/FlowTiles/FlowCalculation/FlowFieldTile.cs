using System;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowField {
    public struct FlowFieldTile {

        public int2 Size;
        public NativeArray<double2> Directions;
        public NativeArray<double> Gradients;
        public TimeSpan GenerationTime;

        public void Dispose() {
            Directions.Dispose();
            Gradients.Dispose();
        }

    }

}