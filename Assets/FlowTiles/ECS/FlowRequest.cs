﻿using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {
    public struct FlowRequest : IBufferElementData {

        public int2 goalCell;
        public int2 goalDirection;
        public int4 cacheKey => new int4(goalCell, goalDirection);

    }

}