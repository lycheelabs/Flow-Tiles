using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    // Update this component to start/stop pathfinding or change your destination.
    public struct FlowGoal : IComponentData {

        public bool HasGoal;
        
        public int2 Goal;
        public int TravelType;
        public PathSmoothingMode SmoothingMode;

    }

}