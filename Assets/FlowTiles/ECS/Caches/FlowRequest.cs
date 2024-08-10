using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct FlowRequest : IBufferElementData {

        public int2 goalCell;
        public int2 goalDirection;
        public int travelType;
        public int4 cacheKey;

    }

}