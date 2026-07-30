using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.PortalPaths {

    public struct SectorData {

        public readonly int Index;
        public readonly int TravelType;
        public readonly CellRect Bounds;
        public readonly int Version;

        public SectorCosts Costs;
        public SectorIslands Islands;
        public SectorPortals Portals;
        public bool IsCreated;

        public SectorData (int index, CellRect boundaries, int travelType, int version) {
            Index = index;
            TravelType = travelType;
            Bounds = boundaries;
            Version = version;

            Costs = new SectorCosts(index, boundaries, travelType);
            Islands = new SectorIslands(index, boundaries);
            Portals = new SectorPortals(index, boundaries);
            IsCreated = true;
        }

        public void Initialise(ref PathableGrid levelGrid) {
            Costs.Initialise(levelGrid);

            if (Bounds.MinCell.x > 0) {
                BuildPortals(ref levelGrid, Bounds.MinCell.x, Bounds.MinCell.y, horizontal: true, check: -1);
            }
            if (Bounds.MinCell.y > 0) {
                BuildPortals(ref levelGrid, Bounds.MinCell.x, Bounds.MinCell.y, horizontal: false, check: -1);
            }
            if (Bounds.MaxCell.x < levelGrid.Bounds.MaxCell.x) {
                BuildPortals(ref levelGrid, Bounds.MaxCell.x, Bounds.MinCell.y, horizontal: true, check: +1);
            }
            if (Bounds.MaxCell.y < levelGrid.Bounds.MaxCell.y) {
                BuildPortals(ref levelGrid, Bounds.MinCell.x, Bounds.MaxCell.y, horizontal: false, check: +1);
            }
        }

        // Check is +1 or -1, indicating which neighbor we are checking against
        private void BuildPortals(ref PathableGrid levelCosts, int xStart, int yStart, bool horizontal, int check) {

            int lineSize = 0;
            var portalCost1 = 0;
            var portalCost2 = 0;

            int i, iMin, iMax, j1, j2;
            int2 sampleNeighborCell;
            if (horizontal) {
                iMin = Bounds.MinCell.y;
                iMax = Bounds.MaxCell.y;
                j1 = xStart;
                j2 = xStart + check;
                sampleNeighborCell = new int2(j2, iMin);
            }
            else {
                iMin = Bounds.MinCell.x;
                iMax = Bounds.MaxCell.x;
                j1 = yStart;
                j2 = yStart + check;
                sampleNeighborCell = new int2(iMin, j2);
            }

            var neighborSectorIndex = levelCosts.Layout.CellToSectorIndex(sampleNeighborCell);
            if (neighborSectorIndex < 0 || neighborSectorIndex >= levelCosts.Layout.NumSectorsInLevel) {
                return;
            }

            for (i = iMin; i <= iMax; i++) {
                var cell1 = horizontal ? new int2(j1, i) : new int2(i, j1);
                var cell2 = horizontal ? new int2(j2, i) : new int2(i, j2);

                var oldCost1 = portalCost1;
                var oldCost2 = portalCost2;
                portalCost1 = levelCosts.GetCostAt(cell1.x, cell1.y, TravelType);
                portalCost2 = levelCosts.GetCostAt(cell2.x, cell2.y, TravelType);

                var bothSidesOpen = portalCost1 < PathableGrid.MAX_COST && portalCost2 < PathableGrid.MAX_COST;
                if (bothSidesOpen) {
                    if (lineSize == 0 || (portalCost1 == oldCost1 && portalCost2 == oldCost2)) {
                        lineSize++;
                        continue;
                    }
                }
                if (lineSize > 0) {
                    Portals.CreatePortal(neighborSectorIndex, horizontal, lineSize, i, check);
                    lineSize = 0;
                    if (bothSidesOpen) {
                        lineSize++;
                    }
                }
            }

            if (lineSize > 0) {
                Portals.CreatePortal(neighborSectorIndex, horizontal, lineSize, i, check);
            }
        }


        public void Dispose() {
            if (!IsCreated) return;
            
            Costs.Dispose();
            Islands.Dispose();
            Portals.Dispose();
            IsCreated = false;
        }

        public int GetCellIsland(int2 cell) {
            if (!Bounds.ContainsCell(cell)) return -1;
            var localCell = cell - Bounds.MinCell;
            if (localCell.x < 0 || localCell.x >= Islands.Cells.Size.x || localCell.y < 0 || localCell.y >= Islands.Cells.Size.y) {
                return -1;
            }
            return Islands.Cells[localCell.x, localCell.y];
        }

        public SectorRoot GetRoot(int2 cell) {
            if (!Bounds.ContainsCell(cell)) {
                return default;
            }
            var localCell = cell - Bounds.MinCell;
            if (localCell.x < 0 || localCell.x >= Islands.Cells.Size.x || localCell.y < 0 || localCell.y >= Islands.Cells.Size.y) {
                return default;
            }
            var island = Islands.Cells[localCell.x, localCell.y];
            if (island <= 0 || island > Portals.Roots.Length) {
                return default;
            }
            return Portals.Roots[island - 1];
        }

        public bool TryGetPortal(int2 cell, out Portal portal) {
            if (!Portals.HasExitPortalAt(cell)) {
                portal = default;
                return false;
            }
            portal = Portals.GetExitPortalAt(cell);
            return true;
        }

        public Portal GetPortal(int2 cell) {
            return Portals.GetExitPortalAt(cell);
        }

        public void SetPortal(int2 cell, Portal portal) {
            Portals.SetExitPortalAt(cell, portal);
        }
    }

}