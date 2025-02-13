using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct SectorPortals {

        public readonly int Index;
        public readonly CellRect Bounds;

        public UnsafeList<SectorRoot> Roots;
        public UnsafeList<Portal> Portals;
        public UnsafeHashMap<int2, int> PortalLookup;

        public SectorPortals (int index, CellRect bounds) {
            Index = index;
            Bounds = bounds;

            Roots = new UnsafeList<SectorRoot>(Constants.EXPECTED_MAX_ISLANDS, Allocator.Persistent);
            Portals = new UnsafeList<Portal>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
            PortalLookup = new UnsafeHashMap<int2, int>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
        }

        public void Clear() {
            DisposePortals();

            Roots.Clear();
            Portals.Clear();
            PortalLookup.Clear();
        }

        public void Dispose() {
            DisposePortals();

            Roots.Dispose();
            Portals.Dispose();
            PortalLookup.Dispose();
        }

        private void DisposePortals() {
            for (int i = 0; i < Roots.Length; i++) {
                Roots[i].Dispose();
            }
            for (int i = 0; i < Portals.Length; i++) {
                Portals[i].Dispose();
            }
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool HasExitPortalAt(int2 pos) {
            return PortalLookup.ContainsKey(pos);
        }

        public Portal GetExitPortalAt(int2 pos) {
            var index = PortalLookup[pos];
            return Portals[index];
        }

        public void CreatePortal(int targetSector, bool horizontal, int lineSize, int i, int flip) {
            var start = i - lineSize;
            var end = i - 1;

            int2 start1, start2, end1, end2;
            if (horizontal) {
                var x1 = (flip > 0) ? Bounds.MaxCell.x : Bounds.MinCell.x;
                var x2 = x1 + flip;
                start1 = new int2(x1, start);
                end1 = new int2(x1, end);
                start2 = new int2(x2, start);
                end2 = new int2(x2, end);
            }
            else {
                var y1 = (flip > 0) ? Bounds.MaxCell.y : Bounds.MinCell.y;
                var y2 = y1 + flip;
                start1 = new int2(start, y1);
                end1 = new int2(end, y1);
                start2 = new int2(start, y2);
                end2 = new int2(end, y2);
            }

            var mid1 = (start1 + end1) / 2;
            var mid2 = (start2 + end2) / 2;

            // Create the exit portal (if needed)
            if (!HasExitPortalAt(mid1)) {
                var newPortal = new Portal(start1, end1, Index);
                PortalLookup.Add(newPortal.Center.Cell, Portals.Length);
                Portals.Add(newPortal);
            }

            // Connect the exit portal to the adjacent exit (which may not be created yet)
            var portalIndex = PortalLookup[mid1];
            var portal = Portals[portalIndex];
            portal.Edges.Add(new PortalEdge() {
                start = new SectorCell(Index, mid1),
                end = new SectorCell(targetSector, mid2),
                weight = 1
            });
            Portals[portalIndex] = portal;
        }

        /// <summary>
        /// Connect all exit portals inside this sector together (if their colors match)
        /// and build a root cluster
        /// </summary>
        public void BuildInternalConnections (SectorData sector, SectorPathfinder pathfinder) {
            SetPortalIslands(sector);
            BuildRootConnections(sector);
            BuildExitConnections(sector, pathfinder);
        }

        // --------------------------------------------------------------

        private void SetPortalIslands(SectorData sector) {
            for (int i = 0; i < Portals.Length; i++) {
                var portal = Portals[i];
                var tile = portal.Center.Cell - Bounds.MinCell;
                portal.Island = sector.Islands.Cells[tile.x, tile.y];
                Portals[i] = portal;
            }
        }

        private void BuildRootConnections (SectorData sector) {

            // Create a root for each island
            for (int island = 1; island <= sector.Islands.NumIslands; island++) {
                var root = new SectorRoot(Index, island);

                // Connect all portals in this island
                for (int p = 0; p < Portals.Length; p++) {
                    var portal = Portals[p];
                    if (portal.Island == island) {
                        root.Portals.Add(portal.Center);
                    }
                }

                Roots.Add(root);
            }

        }

        private void BuildExitConnections (SectorData sector, SectorPathfinder pathfinder) {
            int i, j;
            Portal n1, n2;

            // We do this so that we can iterate through pairs once, 
            // by keeping the second index always higher than the first.
            // For a path to exit, both portals must have matching color.        
            for (i = 0; i < Portals.Length; ++i) {
                n1 = Portals[i];
                for (j = i + 1; j < Portals.Length; ++j) {
                    n2 = Portals[j];
                    if (n1.Island == n2.Island) {
                        TryConnectExits(ref n1, ref n2, sector.Costs, pathfinder);
                        Portals[i] = n1;
                        Portals[j] = n2;
                    }
                }
            }
        }

        private bool TryConnectExits(ref Portal n1, ref Portal n2, SectorCosts sector, SectorPathfinder pathfinder) {
            PortalEdge e1, e2;

            var corner = sector.Bounds.MinCell;
            var pathCost = pathfinder.FindTravelCost(
                sector.Cells, n1.Center.Cell - corner, n2.Center.Cell - corner);

            if (pathCost > 0) {
                e1 = new PortalEdge() {
                    start = n1.Center,
                    end = n2.Center,
                    weight = pathCost,
                };

                e2 = new PortalEdge() {
                    start = n2.Center,
                    end = n1.Center,
                    weight = pathCost,
                };

                n1.Edges.Add(e1);
                n2.Edges.Add(e2);

                return true;
            }
            return false;
        }

    }

}