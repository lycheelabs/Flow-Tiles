using Unity.Collections;

namespace FlowTiles.PortalGraphs {
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

        public void Build(PathableLevel map) {
            for (int x = 0; x < Layout.SizeSectors.x; x++) {
                for (int y = 0; y < Layout.SizeSectors.y; y++) {
                    var index = Layout.IndexOfSector(x, y);
                    var bounds = Layout.GetSectorBounds(x, y);
                    var sector = new CostSector(index, bounds);
                    sector.Build(map);
                    Sectors[index] = sector;
                }
            }
        }

    }

}