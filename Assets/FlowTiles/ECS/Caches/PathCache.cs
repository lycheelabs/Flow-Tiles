using Unity.Mathematics;
using Unity.Collections;

namespace FlowTiles.ECS {

    public struct PathCache {

        public NativeParallelHashMap<int4, CachedPortalPath> Cache;
        
    }

}