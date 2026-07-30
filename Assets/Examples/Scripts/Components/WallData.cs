using Unity.Entities;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.Examples {
    public struct WallData : IComponentData {

        public int2 cell;
        public bool isWall;

    }

}