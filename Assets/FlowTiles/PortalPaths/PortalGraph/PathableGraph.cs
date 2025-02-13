using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    [BurstCompile]
    public struct PathableGraph {

        // Note: Top level data structures must be NativeContainers.
        // Nested data structures must be UnsafeContainers.

        public CellRect Bounds;
        public SectorLayout Layout;
        private NativeArray<GraphSector> Sectors;
        public int NumTravelTypes;

        public NativeReference<int> GraphVersion;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(int width, int height, int resolution, int numTravelTypes) {
            var sizeCells = new int2(width, height);
            Bounds = new CellRect(0, sizeCells - 1);
            Layout = new SectorLayout(sizeCells, resolution);
            Sectors = new NativeArray<GraphSector>(Layout.NumSectorsInLevel, Allocator.Persistent);
            NumTravelTypes = numTravelTypes;
            GraphVersion = new NativeReference<int>(Allocator.Persistent);
        }

        public void Dispose() {
            for (int i = 0; i < Sectors.Length; i++) {
                Sectors[i].Dispose();
            }
            Sectors.Dispose();
            GraphVersion.Dispose();
        }

        public bool SectorIsInitialised(int index) {
            return Sectors[index].IsCreated;
        }

        public int CellToIndex(int2 cell) {
            var sectorX = cell.x / Layout.Resolution;
            var sectorY = cell.y / Layout.Resolution;
            return sectorX + sectorY * Layout.SizeSectors.x;
        }

        public GraphSector IndexToSector(int index) {
            return Sectors[index];
        }

        public GraphSector CellToSector(int2 pos) {
            return Sectors[CellToIndex(pos)];
        }

        public int CellToCost(int2 pos, int travelType) {
            var sector = CellToSectorMap(pos, travelType);
            var corner = sector.Bounds.MinCell;
            return sector.Costs.Cells[pos.x - corner.x, pos.y - corner.y];
        }

        public SectorData IndexToSectorMap (int index, int travelType) {
            var sector = Sectors[index];
            var data = sector.GetData(travelType);
            return data;
        }

        public SectorData CellToSectorMap (int2 pos, int travelType) {
            var sectorX = pos.x / Layout.Resolution;
            var sectorY = pos.y / Layout.Resolution;
            var sector = Sectors[sectorX + sectorY * Layout.SizeSectors.x];
            var data = sector.GetData(travelType);
            return data;
        }

        public void ReinitialiseSector(int index, PathableLevel level) {
            int sectorVersion = Sectors[index].Version + 1;
            Sectors[index].Dispose();

            Sectors[index] = new GraphSector(index, sectorVersion, Layout.GetSectorBounds(index), level, NumTravelTypes);
            for (int travelType = 0; travelType < NumTravelTypes; travelType++) {
                Sectors[index].GetData(travelType).Initialise(level);
            }
        }

        public void BuildPortals(int index) {
            var x = index % Layout.SizeSectors.x;
            var y = index / Layout.SizeSectors.x;
            if (x < Layout.SizeSectors.x - 1) {
                BuildEdgePortals(index, index + 1, true, 1);
            }
            if (x > 0) {
                BuildEdgePortals(index, index - 1, true, -1);
            }
            if (y < Layout.SizeSectors.y - 1) {
                BuildEdgePortals(index, index + Layout.SizeSectors.x, false, 1);
            }
            if (y > 0) {
                BuildEdgePortals(index, index - Layout.SizeSectors.x, false, -1);
            }
        }

        private void BuildEdgePortals(int index1, int index2, bool horizontal, int flip) {
            for (int travelType = 0; travelType < NumTravelTypes; travelType++) {
                var costs1 = Sectors[index1].GetData(travelType).Costs;
                var costs2 = Sectors[index2].GetData(travelType).Costs;
                var bounds = costs1.Bounds;

                var data = Sectors[index1].GetData(travelType);
                var portals = data.Portals;
                int lineSize = 0;
                var portalCost1 = 0;
                var portalCost2 = 0;

                int i, iMin, iMax, j1, j2;
                if (horizontal) {
                    iMin = bounds.MinCell.y;
                    iMax = bounds.MaxCell.y;
                    j1 = (flip > 0) ? costs1.Bounds.MaxCell.x : costs1.Bounds.MinCell.x;
                    j2 = (flip > 0) ? costs2.Bounds.MinCell.x : costs2.Bounds.MaxCell.x;
                } else {
                    iMin = bounds.MinCell.x;
                    iMax = bounds.MaxCell.x;
                    j1 = (flip > 0) ? costs1.Bounds.MaxCell.y : costs1.Bounds.MinCell.y;
                    j2 = (flip > 0) ? costs2.Bounds.MinCell.y : costs2.Bounds.MaxCell.y;
                }

                for (i = iMin; i <= iMax; i++) {
                    var cell1 = horizontal ? new int2(j1, i) : new int2(i, j1);
                    var cell2 = horizontal ? new int2(j2, i) : new int2(i, j2);

                    var oldCost1 = portalCost1;
                    var oldCost2 = portalCost2;
                    portalCost1 = costs1.GetCostAt(cell1);
                    portalCost2 = costs2.GetCostAt(cell2);

                    var bothSidesOpen = portalCost1 < PathableLevel.MAX_COST && portalCost2 < PathableLevel.MAX_COST;
                    if (bothSidesOpen) {
                        if (lineSize == 0 || (portalCost1 == oldCost1 && portalCost2 == oldCost2)) {
                            lineSize++;
                            continue;
                        }
                    }
                    if (lineSize > 0) {
                        portals.CreatePortal(index2, horizontal, lineSize, i, flip);
                        lineSize = 0;
                        if (bothSidesOpen) {
                            lineSize++;
                        }
                    }
                }

                if (lineSize > 0) {
                    portals.CreatePortal(index2, horizontal, lineSize, i, flip);
                }

                data.Portals = portals;
                Sectors[index1].UpdateData(travelType, data);
            }
        }

        public void BuildSector(int index) {
            SectorPathfinder pathfinder = new SectorPathfinder(Layout.NumCellsInSector, Allocator.Temp);
            var sector = Sectors[index];
            for (int travelType = 0; travelType < NumTravelTypes; travelType++) {
                var data = sector.GetData(travelType);
                data.Islands.CalculateIslands(data.Costs);
                data.Portals.BuildInternalConnections(data, pathfinder);
                sector.UpdateData(travelType, data);
            }
        }

    }

}