using FlowTiles.FlowFields;
using Unity.Entities;

namespace FlowTiles.ECS {

    // Only attach this component to visualise flow data for debugging
    public struct FlowDebugData : IComponentData {

        public FlowField CurrentFlowTile;

    }

}