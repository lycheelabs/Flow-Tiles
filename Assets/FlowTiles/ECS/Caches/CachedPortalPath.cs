using Unity.Collections.LowLevel.Unsafe;
using FlowTiles.PortalPaths;
using System;

namespace FlowTiles.ECS {
    public struct CachedPortalPath {

        public bool IsPending;
        public bool HasBeenQueued;

        public bool NoPathExists;
        public int GraphVersionAtSearch;
        public UnsafeList<PortalPathNode> Nodes;

        public void Dispose() {
            Nodes.Dispose();
        }

    }

}