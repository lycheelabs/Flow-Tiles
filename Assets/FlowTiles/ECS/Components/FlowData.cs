using Unity.Entities;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.ECS {

    public struct FlowData : IComponentData {

        public int2 cell;
        public bool isChunk;

    }

}