using System;
using Unity.Collections;

namespace FlowTiles.PortalPaths {

    public struct CostMap {

        public readonly SectorLayout Layout;
        public NativeArray<CostSector> Sectors;

        public CostMap(SectorLayout layout) {
            Layout = layout;
            Sectors = new NativeArray<CostSector>(Layout.NumSectorsInLevel, Allocator.Persistent);
        }

        public int GetCellIndex(int cellX, int cellY) {
            return Layout.IndexOfCell(cellX, cellY);
        }

        public int GetSectorIndex(int cellX, int cellY) {
            return Layout.CellToSectorIndex(cellX, cellY);
        }

        public CostSector GetSector(int cellX, int cellY) {
            return Sectors[Layout.CellToSectorIndex(cellX, cellY)];
        }

        public int GetColor(int cellX, int cellY) {
            var sector = GetSector(cellX, cellY);
            var tileX = cellX % Layout.Resolution;
            var tileY = cellY % Layout.Resolution;
            return sector.Colors[tileX, tileY];
        }

        public void Initialise(PathableLevel map) {
            for (int index = 0; index < Layout.NumSectorsInLevel; index++) {
                InitialiseSector(index, map);
            }
        }

        public void InitialiseSector(int index, PathableLevel map) {
            var x = index % Layout.SizeSectors.x;
            var y = index / Layout.SizeSectors.x;
            var bounds = Layout.GetSectorBounds(x, y);
            var sector = new CostSector(index, bounds);
            sector.Initialise(map);
            Sectors[index] = sector;
        }

        public void CalculateColors() {
            for (int index = 0; index < Layout.NumSectorsInLevel; index++) {
                var sector = Sectors[index];
                sector.Process();
                Sectors[index] = sector;
            }
        }

    }

}