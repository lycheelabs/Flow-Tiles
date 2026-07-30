using Unity.Entities;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.ECS {

    public struct FlowProgress : IComponentData {

        // Follow the path
        public bool IsAttachedToPath;
        public int4 PathKey;
        public int NodeIndex;

        // Follow the flow
        public bool IsAttachedToFlow;
        public int4 FlowKey;

        // Follow the sightline
        public int4 KnownSightlineKey;
        public int4 NewSightlineKey;

    }

}