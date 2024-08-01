using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {

    public struct PathfindingGoal : IComponentData {

        public int2 OriginCell;
        public int2 DestCell;

    }

    public struct PathfindingResult : IComponentData {

        public float2 PathDirection;

    }

}