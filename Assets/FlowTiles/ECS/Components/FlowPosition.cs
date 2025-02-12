using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace FlowTiles.ECS {

    // This component lets the pathfinder know which tile to search from.
    // Important: Update this every time the agent moves!
    public struct FlowPosition : IComponentData {

        public float2 Position;
        public int2 PositionCell => new int2(
            (int) math.round(Position.x), 
            (int) math.round(Position.y));

    }

}