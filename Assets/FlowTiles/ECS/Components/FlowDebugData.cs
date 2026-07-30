using LycheeLabs.FlowTiles.FlowFields;
using Unity.Entities;

namespace LycheeLabs.FlowTiles.ECS {

    // Only attach this component to visualise flow data for debugging
    public struct FlowDebugData : IComponentData {

        public FlowField CurrentFlowTile;

    }

}