using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace FlowTiles {
    public struct PathCache : IComponentData {

        // int4 = (source.xy, dest.xy)
        public NativeParallelHashMap<int4, PortalPath> Cache;
        

    }

    public struct PortalPath : IComponentData {

        //public DynamicBuffer<Portal> nodes;

    }

}