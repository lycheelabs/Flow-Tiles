using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {

    // This component lets the pathfinder know which tile to search from.
    // Important: Update this every time the agent moves!
    public struct FlowPosition : IComponentData {

        public int2 Position;

    }

    // Update this component to start/stop pathfinding or change your destination.
    public struct FlowGoal : IComponentData {

        public bool HasGoal;
        public int2 Goal;

    }

    // This component
    public struct FlowProgress : IComponentData {

        // Follow the path
        public bool HasPath;
        public int4 PathKey;
        public int NodeIndex;

        // Follow the flow
        public bool HasFlow;
        public int4 FlowKey;

    }

    // This component returns the current flow direction for use by other systems
    public struct FlowDirection : IComponentData {

        public float2 Direction;

    }

    // Only attach this component to visualise flow data for debugging
    public struct FlowDebugData : IComponentData {

        public FlowFieldTile CurrentFlowTile;

    }

}