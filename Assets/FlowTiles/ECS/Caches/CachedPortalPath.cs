using Unity.Collections.LowLevel.Unsafe;
using FlowTiles.PortalPaths;

namespace FlowTiles.ECS {
    public struct CachedPortalPath {

        public bool IsPending;
        public bool NoPathExists;
        public UnsafeList<PortalPathNode> Nodes;

    }

}