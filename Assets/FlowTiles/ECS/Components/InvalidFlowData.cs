using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct InvalidFlowData : IComponentData {

        public int4 Key;

    }
}