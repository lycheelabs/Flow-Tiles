using FlowTiles.PortalPaths;
using Unity.Entities;

namespace FlowTiles.ECS {
    public struct GlobalPathfindingData : IComponentData {

        public PathableLevel Level;
        public PathableGraph Graph;

    }
}