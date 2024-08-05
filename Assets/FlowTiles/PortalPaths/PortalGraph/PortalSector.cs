using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalSector {

        public readonly int Index;
        public readonly CellRect Bounds;

        public UnsafeList<Portal> RootPortals;
        public UnsafeList<Portal> ExitPortals;
        public UnsafeHashMap<int2, int> ExitPortalLookup;

        public PortalSector (int index, CellRect bounds) {
            Index = index;
            Bounds = bounds;

            RootPortals = new UnsafeList<Portal>(Constants.EXPECTED_MAX_COLORS, Allocator.Persistent);
            ExitPortals = new UnsafeList<Portal>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
            ExitPortalLookup = new UnsafeHashMap<int2, int>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
        }

        public void Clear() {
            RootPortals.Clear();
            ExitPortals.Clear();
            ExitPortalLookup.Clear();
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool HasExitPortalAt(int2 pos) {
            return ExitPortalLookup.ContainsKey(pos);
        }

        public Portal GetExitPortalAt(int2 pos) {
            var index = ExitPortalLookup[pos];
            return ExitPortals[index];
        }

        /// <summary>
        /// Returns the closest reachable portal to this point, if one exists
        /// </summary>
        public bool TryGetClosestExitPortal(int2 pos, int color, out Portal closest) {
            if (ExitPortals.Length == 0) {
                closest = default;
                return false;
            }

            closest = ExitPortals[0];
            var found = false;
            var bestDist = 0f;

            for (int i = 0; i < ExitPortals.Length; i++) {
                var portal = ExitPortals[i];
                if (portal.Color != color) {
                    continue;
                }

                var dist = math.distancesq(portal.Position.Cell, pos);
                if (!found || dist < bestDist) {
                    closest = portal;
                    bestDist = dist;
                    found = true;
                }
            }
            return found;
        }

        public void CreateExit(int targetSector, bool horizontal, int lineSize, int i, int flip) {
            if (lineSize <= 0) { return; }
            var start = i - lineSize;
            var end = i - 1;

            int2 start1, start2, end1, end2, dir;
            if (horizontal) {
                var x1 = (flip > 0) ? Bounds.MaxCell.x : Bounds.MinCell.x;
                var x2 = x1 + flip;
                start1 = new int2(x1, start);
                end1 = new int2(x1, end);
                start2 = new int2(x2, start);
                end2 = new int2(x2, end);
                dir = new int2(flip, 0);
            }
            else {
                var y1 = (flip > 0) ? Bounds.MaxCell.y : Bounds.MinCell.y;
                var y2 = y1 + flip;
                start1 = new int2(start, y1);
                end1 = new int2(end, y1);
                start2 = new int2(start, y2);
                end2 = new int2(end, y2);
                dir = new int2(0, flip);
            }

            var mid1 = (start1 + end1) / 2;
            var mid2 = (start2 + end2) / 2;

            // Create the exit portal (if needed)
            if (!HasExitPortalAt(mid1)) {
                var newPortal = new Portal(start1, end1, Index, dir);
                ExitPortalLookup.Add(newPortal.Position.Cell, ExitPortals.Length);
                ExitPortals.Add(newPortal);
            }

            // Connect the exit portal to the adjacent exit (which may not be created yet)
            var portalIndex = ExitPortalLookup[mid1];
            var portal = ExitPortals[portalIndex];
            portal.Edges.Add(new PortalEdge() {
                start = new SectorCell(Index, mid1),
                end = new SectorCell(targetSector, mid2),
                weight = 1
            });
            ExitPortals[portalIndex] = portal;
        }

        /// <summary>
        /// Connect all exit portals inside this sector together (if their colors match)
        /// and build a root cluster
        /// </summary>
        public void BuildInternalConnections (CostSector costs, SectorPathfinder pathfinder) {
            ColorExitPortals(costs);
            BuildRootConnections(costs);
            BuildExitConnections(costs, pathfinder);
        }

        // --------------------------------------------------------------

        private void ColorExitPortals(CostSector costs) {
            for (int i = 0; i < ExitPortals.Length; i++) {
                var portal = ExitPortals[i];
                var tile = portal.Position.Cell - Bounds.MinCell;
                var color = costs.Colors[tile.x, tile.y];
                portal.Color = color;
                ExitPortals[i] = portal;
            }
        }

        private void BuildRootConnections (CostSector costs) {

            // Color the edge portals
            for (int i = 0; i < ExitPortals.Length; i++) {
                var portal = ExitPortals[i];
                var tile = portal.Position.Cell - Bounds.MinCell;
                var color = costs.Colors[tile.x, tile.y];
                portal.Color = color;
                ExitPortals[i] = portal;
            }

            // Create the color roots
            for (int color = 1; color <= costs.NumColors; color++) {
                var colorPortal = new Portal(Bounds.CentreCell, Index, 0);
                colorPortal.Color = color;

                for (int p = 0; p < ExitPortals.Length; p++) {
                    var portal = ExitPortals[p];
                    if (portal.Color == color) {
                        colorPortal.Edges.Add(new PortalEdge {
                            start = colorPortal.Position,
                            end = portal.Position,
                            weight = 0,
                        });
                    }
                }

                RootPortals.Add(colorPortal);
            }

        }

        private void BuildExitConnections (CostSector costs, SectorPathfinder pathfinder) {
            int i, j;
            Portal n1, n2;

            // We do this so that we can iterate through pairs once, 
            // by keeping the second index always higher than the first.
            // For a path to exit, both portals must have matching color.        
            for (i = 0; i < ExitPortals.Length; ++i) {
                n1 = ExitPortals[i];
                for (j = i + 1; j < ExitPortals.Length; ++j) {
                    n2 = ExitPortals[j];
                    if (n1.Color == n2.Color) {
                        TryConnectExits(ref n1, ref n2, costs, pathfinder);
                        ExitPortals[i] = n1;
                        ExitPortals[j] = n2;
                    }
                }
            }
        }

        private bool TryConnectExits(ref Portal n1, ref Portal n2, CostSector sector, SectorPathfinder pathfinder) {
            PortalEdge e1, e2;

            var corner = sector.Bounds.MinCell;
            var pathCost = pathfinder.FindTravelCost(
                sector.Costs, n1.Position.Cell - corner, n2.Position.Cell - corner);

            if (pathCost > 0) {
                e1 = new PortalEdge() {
                    start = n1.Position,
                    end = n2.Position,
                    weight = pathCost,
                };

                e2 = new PortalEdge() {
                    start = n2.Position,
                    end = n1.Position,
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