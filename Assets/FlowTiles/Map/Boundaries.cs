using Unity.Mathematics;

namespace FlowTiles {

    public struct Boundaries {

        public int2 Min;
        public int2 Max;

        public Boundaries (int2 min, int2 max) {
            Min = min; 
            Max = max;

            if (min.x > max.x || min.y > max.y) {
                throw new System.ArgumentException("The provided min boundary exceeds the max boundary");
            }
        }

        public int Width => Max.x - Min.x + 1;
        public int Height => Max.y - Min.y + 1;
        public int2 Size => Max - Min + 1;
        public int2 CentreCell => (Min + Max) / 2;
        public float2 CentrePoint => (float2)(Min + Max + 1) / 2f;

    }

}