using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {

    public struct AgentData : IComponentData {

        public float2 position;
        public float2 speed;

    }

}