using FlowTiles.PortalPaths;
using Unity.Entities;

namespace FlowTiles.ECS {
    public struct GlobalPathfindingData : IComponentData {

        public bool IsInitialised;
        public PathableLevel Level;
        public PathableGraph Graph;

    }
}