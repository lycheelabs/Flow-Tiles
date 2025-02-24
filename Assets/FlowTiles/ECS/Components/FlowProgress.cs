using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    public struct FlowProgress : IComponentData {

        // Follow the path
        public bool HasPath;
        public int4 PathKey;
        public int NodeIndex;

        // Follow the flow
        public bool HasFlow;
        public int4 FlowKey;

        // Follow the sightline
        public int4 KnownSightlineKey;
        public int4 NewSightlineKey;

    }

}