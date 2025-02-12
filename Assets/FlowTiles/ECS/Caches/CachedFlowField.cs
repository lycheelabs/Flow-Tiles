using System;
using FlowTiles.FlowFields;

namespace FlowTiles.ECS {
    public struct CachedFlowField {

        public bool IsPending;
        public bool HasBeenQueued;

        public FlowField FlowField;

        public void Dispose() {
            FlowField.Dispose();
        }

    }
}