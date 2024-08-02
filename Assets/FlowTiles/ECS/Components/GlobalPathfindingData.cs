using FlowTiles.PortalPaths;
using Unity.Entities;

namespace FlowTiles.ECS {
    public struct GlobalPathfindingData : IComponentData {

        public PathableGraph Graph;

    }
}