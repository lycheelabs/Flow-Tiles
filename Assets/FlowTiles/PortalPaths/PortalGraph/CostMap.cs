using System;
using Unity.Collections;

namespace FlowTiles.PortalPaths {
    /*
    public struct CostMap {

        public readonly SectorLayout Layout;
        public NativeArray<CostMap> Sectors;

        public CostMap(SectorLayout layout) {
            Layout = layout;
            Sectors = new NativeArray<CostMap>(Layout.NumSectorsInLevel, Allocator.Persistent);
            
            for (int index = 0; index < Layout.NumSectorsInLevel; index++) {
                var x = index % Layout.SizeSectors.x;
                var y = index / Layout.SizeSectors.x;
                var bounds = Layout.GetSectorBounds(x, y);
                var sector = new CostMap(index, bounds);
                Sectors[index] = sector;
            }
        }

        public int GetCellIndex(int cellX, int cellY) {
            return Layout.IndexOfCell(cellX, cellY);
        }

        public int GetSectorIndex(int cellX, int cellY) {
            return Layout.CellToSectorIndex(cellX, cellY);
        }

        public CostMap GetSector(int cellX, int cellY) {
            return Sectors[Layout.CellToSectorIndex(cellX, cellY)];
        }

        public int GetColor(int cellX, int cellY) {
            var sector = GetSector(cellX, cellY);
            var tileX = cellX % Layout.Resolution;
            var tileY = cellY % Layout.Resolution;
            return sector.Colors[tileX, tileY];
        }

        public void InitialiseSector(int index, PathableLevel map) {
            var sector = Sectors[index];
            sector.Initialise(map);
            Sectors[index] = sector;
        }

    }
    */
}