using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using static FlowTiles.PortalPathfinder;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowTiles {

    public struct PathCache : IComponentData {

        // int4 = (source.xy, dest.xy)
        public NativeParallelHashMap<int4, CachedPath> Cache;
        

    }

    public struct CachedPath {

        public UnsafeList<PortalPathNode> Path;

    }

}