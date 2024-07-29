using Unity.Mathematics;

namespace FlowTiles {

    public class Boundaries {

        //Top left corner (minimum corner)
        public int2 Min;

        //Bottom right corner (maximum corner)
        public int2 Max;

        public float2 CentrePoint => new float2(Min.x + Max.x, Min.y + Max.y) / 2f;

    }

}