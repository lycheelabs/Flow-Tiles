using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {

    public struct AgentData : IComponentData {

        public float2 Speed;
        public int TravelType;

    }

}