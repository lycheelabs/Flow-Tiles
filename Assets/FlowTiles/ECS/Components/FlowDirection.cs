using Unity.Entities;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.ECS {

    // This component returns the current flow direction for use by other systems
    public struct FlowDirection : IComponentData {

        public float2 Direction;
        public bool PathIsImpossible;

    }

}