using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct LineRequest : IBufferElementData {

        public int2 startCell;
        public int2 endCell;
        public int2 levelSize;
        public int travelType;

        public int4 CacheKey => CacheKeys.ToPathKey(startCell, endCell, levelSize, travelType);

    }

}