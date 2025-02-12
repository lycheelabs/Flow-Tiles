using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {
    public struct LineOfSightResult : IComponentData {

        public int4 PathKey;
        public bool LineExists;

    }
}