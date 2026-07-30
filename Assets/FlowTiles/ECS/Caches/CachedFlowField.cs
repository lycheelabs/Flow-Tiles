using System;
using FlowTiles.FlowFields;

namespace FlowTiles.ECS {
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