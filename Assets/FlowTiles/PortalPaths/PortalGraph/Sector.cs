using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Sector {

        // Index of the sector, as stored in the graph
        public readonly int Index;

        // Bounds of the sector, relative to the level
        public readonly CellRect Bounds;

        // Portals within this sector
        public NativeList<Portal> RootPortals;
        public NativeList<Portal> ExitPortals;
        public NativeHashMap<int2, int> ExitPortalLookup;

        // Cost and color fields for this sector
        public CostField Costs;
        public ColorField Colors;

        public Sector (int index, CellRect boundaries) {
            Index = index;
            Bounds = new CellRect();
            RootPortals = new NativeList<Portal>(Constants.EXPECTED_MAX_COLORS, Allocator.Persistent);
            ExitPortals = new NativeList<Portal>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
            ExitPortalLookup = new NativeHashMap<int2, int>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);

            Bounds = boundaries;
            Costs = new CostField(Bounds.SizeCells);
            Colors = new ColorField(Bounds.SizeCells);
        }

        public Sector Build(PathableMap map) {
            Costs.Initialise(map, Bounds.MinCell);
            Colors.Recolor(Costs);
            return this;
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool IsOpenAt(int2 pos) {
            if (Contains(pos) 
                && Costs.GetCost(pos - Bounds.MinCell) != CostField.WALL) {
                return true;
            }
            return false;
        }

        public bool HasExitPortalAt(int2 pos) {
            return ExitPortalLookup.ContainsKey(pos);
        }

        public Portal GetExitPortalAt(int2 pos) {
            var index = ExitPortalLookup[pos];
            return ExitPortals[index];
        }


        public void AddExitPortal (Portal portal) {
            ExitPortalLookup.Add(portal.Position.Cell, ExitPortals.Length);
            ExitPortals.Add(portal);
        }

        public void CreateRootPortals () {

            // Color the edge portals
            for (int i = 0; i < ExitPortals.Length; i++) {
                var portal = ExitPortals[i];
                var tile = portal.Position.Cell - Bounds.MinCell;
                var color = Colors.GetColor(tile);
                portal.Color = color;
                ExitPortals[i] = portal;
            }

            // Create the color roots
            for (int color = 1; color <= Colors.NumColors; color++) {
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

    }

}