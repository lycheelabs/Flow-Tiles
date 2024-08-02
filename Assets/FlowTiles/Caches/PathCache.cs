using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using FlowTiles.PortalGraphs;

namespace FlowTiles {

    public struct PathCache {

        public NativeParallelHashMap<int4, PortalPath> Cache;
        
    }

    public struct FlowCache {

        public NativeParallelHashMap<int4, FlowFieldTile> Cache;

    }

    public struct PortalPath {

        public UnsafeList<PortalPathNode> Nodes;

    }

}