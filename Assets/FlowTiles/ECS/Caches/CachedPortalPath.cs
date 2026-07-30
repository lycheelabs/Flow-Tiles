using Unity.Collections.LowLevel.Unsafe;
using LycheeLabs.FlowTiles.PortalPaths;
using System;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.ECS {
    public struct CachedPortalPath {

        public int2 StartCell;
        public UnsafeList<PortalPathNode> Nodes;

        public bool IsPending;
        public bool HasBeenQueued;

        public float PendingSinceTime;
        public int GraphVersionAtSearch;
        public bool PathWasFound;

        public bool IsCreated => Nodes.IsCreated;

        public void Dispose() {
            if (Nodes.IsCreated) {
                Nodes.Dispose();
            }
        }

    }

}