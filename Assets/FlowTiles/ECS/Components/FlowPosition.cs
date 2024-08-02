using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    // This component lets the pathfinder know which tile to search from.
    // Important: Update this every time the agent moves!
    public struct FlowPosition : IComponentData {

        public int2 Position;

    }

}