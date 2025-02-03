using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    // This component returns the current flow direction for use by other systems
    public struct FlowDirection : IComponentData {

        public float2 Direction;
        public float2 NextDirection;

    }

}