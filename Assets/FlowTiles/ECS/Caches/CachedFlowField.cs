using System;
using LycheeLabs.FlowTiles.FlowFields;

namespace LycheeLabs.FlowTiles.ECS {
    public struct CachedFlowField {

        public bool IsPending;
        public bool HasBeenQueued;

        public FlowField FlowField;

        public bool IsCreated => FlowField.IsCreated;

        public void Dispose() {
            if (!IsCreated) return;
            
            FlowField.Dispose();
        }

    }
}