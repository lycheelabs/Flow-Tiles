using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalMap {

        public readonly SectorLayout Layout;
        public NativeArray<PortalSector> Sectors;

        public PortalMap(SectorLayout layout) {
            Layout = layout;
            Sectors = new NativeArray<PortalSector>(Layout.NumSectorsInLevel, Allocator.Persistent);
        }

        public PortalSector GetSector(int cellX, int cellY) {
            return Sectors[Layout.CellToSectorIndex(cellX, cellY)];
        }

        public bool TryGetExitPortal(int cellX, int cellY, out Portal portal) {
            var sector = GetSector(cellX, cellY);
            var key = new int2(cellX, cellY);
            if (!sector.ExitPortalLookup.ContainsKey(key)) {
                portal = default;
                return false;
            }
            var index = sector.ExitPortalLookup[key];
            portal = sector.ExitPortals[index];
            return true;
        }

        public Portal GetRootPortal(int cellX, int cellY, int color) {
            var sector = GetSector(cellX, cellY);
            return sector.RootPortals[color - 1];
        }

        public void Initialise(CostMap mapSectors) {
            for (int i = 0; i < Sectors.Length; i++) {
                InitialiseSector(i);
            }
            for (int i = 0; i < Sectors.Length; i++) {
                BuildExits(i, mapSectors);
            }
        }

        public void InitialiseSector(int index) {
            var x = index % Layout.SizeSectors.x;
            var y = index / Layout.SizeSectors.x;
            var bounds = Layout.GetSectorBounds(x, y);
            var sector = new PortalSector(index, bounds);
            Sectors[index] = sector;
        }

        public void BuildPaths(CostMap mapSectors) {
            var pathfinder = new SectorPathfinder(Layout.NumCellsInSector, Allocator.Temp);
            for (int s = 0; s < Sectors.Length; ++s) {
                var sector = Sectors[s];
                sector.BuildInternalConnections(mapSectors.Sectors[s], pathfinder);
                Sectors[s] = sector;
            }
        }

        // ----------------------------------------------------

        private void BuildExits(int index, CostMap mapSectors) {
            var x = index % Layout.SizeSectors.x;
            var y = index / Layout.SizeSectors.x;
            if (x < Layout.SizeSectors.x - 1) {
                BuildExit(mapSectors, index, index + 1, true, 1);
            }
            if (x > 0) {
                BuildExit(mapSectors, index, index - 1, true, -1);
            }
            if (y < Layout.SizeSectors.y - 1) {
                BuildExit(mapSectors, index, index + Layout.SizeSectors.x, false, 1);
            }
            if (y > 0) {
                BuildExit(mapSectors, index, index - Layout.SizeSectors.x, false, -1);
            }
        }

        private void BuildExit(CostMap costs, int index1, int index2, bool horizontal, int flip) {
            var costs1 = costs.Sectors[index1];
            var costs2 = costs.Sectors[index2];
            var bounds = costs1.Bounds;

            int i = 0; 
            int lineSize = 0;

            var sector = Sectors[index1];
            if (horizontal) {
                int x1 = (flip > 0) ? costs1.Bounds.MaxCell.x : costs1.Bounds.MinCell.x;
                var x2 = (flip > 0) ? costs2.Bounds.MinCell.x : costs2.Bounds.MaxCell.x;
                for (i = bounds.MinCell.y; i <= bounds.MaxCell.y; i++) {
                    if (costs1.IsOpenAt(new int2(x1, i)) && 
                        costs2.IsOpenAt(new int2(x2, i))) {
                        lineSize++;
                        continue;
                    }
                    sector.CreateExit(index2, horizontal, lineSize, i, flip);
                    lineSize = 0;
                }
            }
            if (!horizontal) {
                var y1 = (flip > 0) ? costs1.Bounds.MaxCell.y : costs1.Bounds.MinCell.y;
                var y2 = (flip > 0) ? costs2.Bounds.MinCell.y : costs2.Bounds.MaxCell.y;
                for (i = bounds.MinCell.x; i <= bounds.MaxCell.x; i++) {
                    if (costs1.IsOpenAt(new int2(i, y1)) && 
                        costs2.IsOpenAt(new int2(i, y2))) {
                        lineSize++;
                        continue;
                    }
                    sector.CreateExit(index2, horizontal, lineSize, i, flip);
                    lineSize = 0;
                }
            }

            sector.CreateExit(index2, horizontal, lineSize, i, flip);
            Sectors[index1] = sector;
        }
       
    }

}