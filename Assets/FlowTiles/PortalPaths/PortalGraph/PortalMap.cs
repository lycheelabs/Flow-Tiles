using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalGraphs {

    public struct PortalMap {

        public readonly SectorLayout Layout;
        public NativeArray<PortalSector> Sectors;

        public PortalMap(SectorLayout layout) {
            Layout = layout;
            Sectors = new NativeArray<PortalSector>(Layout.NumSectorsInLevel, Allocator.Persistent);
        }

        public PortalSector GetSector(int cellX, int cellY) {
            return Sectors[Layout.TileToIndex(cellX, cellY)];
        }

        public bool TryGetClusterRoot(int cellX, int cellY, int color, out Portal cluster) {
            if (color <= 0) {
                cluster = default;
                return false;
            }
            var graphSector = GetSector(cellX, cellY);
            cluster = graphSector.RootPortals[color - 1];
            return true;
        }

        public void Build(CostMap mapSectors) {
            BuildSectors();
            LinkSectors(mapSectors);
        }

        private void BuildSectors() {
            for (int x = 0; x < Layout.SizeSectors.x; x++) {
                for (int y = 0; y < Layout.SizeSectors.y; y++) {
                    var index = Layout.SectorToIndex(x, y);
                    var bounds = Layout.GetSectorBounds(x, y);
                    var sector = new PortalSector(index, bounds);
                    Sectors[index] = sector;
                }
            }
        }

        private void LinkSectors(CostMap mapSectors) {

            //Add border nodes for every adjacent pair of GraphSectors
            for (int i = 0; i < Sectors.Length; i++) {

                var x = i % Layout.SizeSectors.x;
                if (x < Layout.SizeSectors.x - 1) {
                    LinkAdjacentSectors(mapSectors, i, i + 1, true);
                }

                var y = i / Layout.SizeSectors.x;
                if (y < Layout.SizeSectors.y - 1) {
                    LinkAdjacentSectors(mapSectors, i, i + Layout.SizeSectors.x, false);
                }

            }

            //Add Intra edges for every border nodes and pathfind between them
            var pathfinder = new SectorPathfinder(Layout.NumCellsInSector, Allocator.Temp);
            for (int s = 0; s < Sectors.Length; ++s) {
                var sector = Sectors[s];
                sector.ConnectExitPortals(mapSectors.Sectors[s], pathfinder);
                Sectors[s] = sector;
            }

            // Create root portals allowing each start tile to reach the same-colored edge portals
            for (int s = 0; s < Sectors.Length; s++) {
                var sector = Sectors[s];
                sector.CreateRootPortals(mapSectors.Sectors[s]);
                Sectors[s] = sector;
            }

        }

        /// <summary>
        /// Create border nodes and attach them together.
        /// We always pass the lower sector first (in sector1).
        /// </summary>
        private void LinkAdjacentSectors(CostMap mapSectors, int index1, int index2, bool horizontal) {
            var sector1 = mapSectors.Sectors[index1];
            var sector2 = mapSectors.Sectors[index2];

            int i, iMin, iMax;
            if (horizontal) {
                iMin = sector1.Bounds.MinCell.y;
                iMax = iMin + sector1.Bounds.HeightCells;
            }
            else {
                iMin = sector1.Bounds.MinCell.x;
                iMax = iMin + sector1.Bounds.WidthCells;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (horizontal && sector1.IsOpenAt(new int2(sector1.Bounds.MaxCell.x, i)) 
                        && sector2.IsOpenAt(new int2(sector2.Bounds.MinCell.x, i))) {
                    lineSize++;
                }
                else if (!horizontal && sector1.IsOpenAt(new int2(i, sector1.Bounds.MaxCell.y)) 
                        && sector2.IsOpenAt(new int2(i, sector2.Bounds.MinCell.y))) {
                    lineSize++;
                }
                else {
                    CreateInterEdges(index1, index2, horizontal, lineSize, i);
                    lineSize = 0;
                }
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateInterEdges(index1, index2, horizontal, lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateInterEdges(int index1, int index2, bool horizontal, int lineSize, int i) {
            if (lineSize > 0) {
                CreateInterEdge(index1, index2, horizontal, i - lineSize, i - 1);// i - (lineSize / 2 + 1));
            }
        }

        //Inter edges are edges that crosses GraphSectors
        private void CreateInterEdge(int index1, int index2, bool horizontal, int start, int end) {
            var sector1 = Sectors[index1];
            var sector2 = Sectors[index2];

            int mid = (start + end) / 2;
            int2 start1, start2, end1, end2, dir;
            Portal portal1, portal2;
            if (horizontal) {
                start1 = new int2(sector1.Bounds.MaxCell.x, start);
                end1 = new int2(sector1.Bounds.MaxCell.x, end);
                start2 = new int2(sector2.Bounds.MinCell.x, start);
                end2 = new int2(sector2.Bounds.MinCell.x, end);
                dir = new int2(1, 0);
            }
            else {
                start1 = new int2(start, sector1.Bounds.MaxCell.y);
                end1 = new int2(end, sector1.Bounds.MaxCell.y);
                start2 = new int2(start, sector2.Bounds.MinCell.y);
                end2 = new int2(end, sector2.Bounds.MinCell.y);
                dir = new int2(0, 1);
            }

            var mid1 = (start1 + end1) / 2;
            if (!sector1.HasExitPortalAt(mid1)) {
                portal1 = new Portal(start1, end1, sector1.Index, dir);
                sector1.AddExitPortal(portal1);
            }

            var mid2 = (start2 + end2) / 2;
            if (!sector2.HasExitPortalAt(mid2)) {
                portal2 = new Portal(start2, end2, sector2.Index, -dir);
                sector2.AddExitPortal(portal2);
            }

            var portalIndex1 = sector1.ExitPortalLookup[mid1];
            portal1 = sector1.ExitPortals[portalIndex1];
            
            var portalIndex2 = sector2.ExitPortalLookup[mid2];
            portal2 = sector2.ExitPortals[portalIndex2];

            portal1.Edges.Add(new PortalEdge() {
                start = portal1.Position,
                end = portal2.Position,
                weight = 1 
            });
            sector1.ExitPortals[portalIndex1] = portal1;

            portal2.Edges.Add(new PortalEdge() {
                start = portal2.Position,
                end = portal1.Position,
                weight = 1 
            });
            sector2.ExitPortals[portalIndex2] = portal2;

            Sectors[index1] = sector1;
            Sectors[index2] = sector2;
        }

    }

}