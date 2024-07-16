using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {
    public struct WallData : IComponentData {

        public int2 cell;
        public bool isWall;

    }

}