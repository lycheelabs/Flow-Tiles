using Unity.Mathematics;

namespace FlowTiles {

    public struct Boundaries {

        public int2 Min;
        public int2 Max;

        public int2 Size => Max - Min + 1;
        public int2 CentreCell => (Min + Max) / 2;
        public float2 CentrePoint => (float2)(Min + Max + 1) / 2f;

        public Boundaries (int2 min, int2 max) {
            Min = min; 
            Max = max;
        }

    }

}