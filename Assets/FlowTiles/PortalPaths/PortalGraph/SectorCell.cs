using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct SectorCell {

        public int SectorIndex;
        public int2 Cell;

        public SectorCell (int sector, int2 cell) {
            SectorIndex = sector;
            Cell = cell;
        }

    }

}