using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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

        public void Build(CostMap mapSectors) {
            BuildSectors();
            LinkSectors(mapSectors);
        }

        private void BuildSectors() {
            for (int x = 0; x < Layout.SizeSectors.x; x++) {
                for (int y = 0; y < Layout.SizeSectors.y; y++) {
                    var index = Layout.IndexOfSector(x, y);
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
                sector.BuildInternalConnections(mapSectors.Sectors[s], pathfinder);
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
            var bounds = sector1.Bounds;

            int i = 0; 
            int lineSize = 0;

            if (horizontal) {
                for (i = bounds.MinCell.y; i <= bounds.MaxCell.y; i++) {
                    if (sector1.IsOpenAt(new int2(sector1.Bounds.MaxCell.x, i)) && 
                        sector2.IsOpenAt(new int2(sector2.Bounds.MinCell.x, i))) {
                        lineSize++;
                        continue;
                    }
                    CreateInterEdge(index1, index2, horizontal, lineSize, i);
                    lineSize = 0;
                }
            }
            if (!horizontal) {
                for (i = bounds.MinCell.x; i <= bounds.MaxCell.x; i++) {
                    if (sector1.IsOpenAt(new int2(i, sector1.Bounds.MaxCell.y)) && 
                        sector2.IsOpenAt(new int2(i, sector2.Bounds.MinCell.y))) {
                        lineSize++;
                        continue;
                    }
                    CreateInterEdge(index1, index2, horizontal, lineSize, i);
                    lineSize = 0;
                }
            }

            CreateInterEdge(index1, index2, horizontal, lineSize, i);
        }
                
        private void CreateInterEdge(int index1, int index2, bool horizontal, int lineSize, int i) {
            if (lineSize <= 0) { return; }
            var start = i - lineSize;
            var end = i - 1;

            var sector1 = Sectors[index1];
            var sector2 = Sectors[index2];

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
                
        private void CreateExit(int index1, int index2, bool horizontal, int lineSize, int i, int flip) {
            if (lineSize <= 0) { return; }
            var start = i - lineSize;
            var end = i - 1;

            var sector1 = Sectors[index1];
            var sector2 = Sectors[index2];

            int2 start1, start2, end1, end2, dir;
            if (horizontal) {
                var x1 = (flip > 0) ? sector1.Bounds.MaxCell.x : sector2.Bounds.MinCell.x;
                var x2 = (flip > 0) ? sector2.Bounds.MinCell.x : sector1.Bounds.MaxCell.x;
                start1 = new int2(x1, start);
                end1 = new int2(x1, end);
                start2 = new int2(x2, start);
                end2 = new int2(x2, end);
                dir = new int2(flip, 0);
            }
            else {
                var y1 = (flip > 0) ? sector1.Bounds.MaxCell.y : sector2.Bounds.MinCell.y;
                var y2 = (flip > 0) ? sector2.Bounds.MinCell.y : sector1.Bounds.MaxCell.y;
                start1 = new int2(start, y1);
                end1 = new int2(end, y1);
                start2 = new int2(start, y2);
                end2 = new int2(end, y2);
                dir = new int2(0, flip);
            }

            var mid1 = (start1 + end1) / 2;
            var mid2 = (start2 + end2) / 2;

            // Create the exit portal (if needed)
            if (!sector1.HasExitPortalAt(mid1)) {
                var newPortal = new Portal(start1, end1, sector1.Index, dir);
                sector1.AddExitPortal(newPortal);
            }

            // Connect the exit portal
            var portalIndex = sector1.ExitPortalLookup[mid1];
            var portal = sector1.ExitPortals[portalIndex];
            portal.Edges.Add(new PortalEdge() {
                start = new SectorCell(sector1.Index, mid1),
                end = new SectorCell(sector2.Index, mid2),
                weight = 1
            });
            sector1.ExitPortals[portalIndex] = portal;
            Sectors[index1] = sector1;
        }

    }

}