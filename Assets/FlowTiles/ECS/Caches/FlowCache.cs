using Unity.Mathematics;
using Unity.Collections;

namespace FlowTiles.ECS {

    public struct FlowCache {

        public NativeParallelHashMap<int4, CachedFlowField> Cache;

    }

}