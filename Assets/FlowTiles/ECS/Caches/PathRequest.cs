
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    public struct PathRequest : IBufferElementData {

        public int2 originCell;
        public int2 destCell;
        public int2 levelSize;
        public int travelType;

        public int4 CacheKey => PathCache.ToKey(originCell, destCell, levelSize, travelType);

    }

}