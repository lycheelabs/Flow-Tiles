using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct GraphSector {

        public readonly int Index;
        public readonly CellRect Bounds;

        public NativeList<Portal> RootPortals;
        public NativeList<Portal> ExitPortals;
        public NativeHashMap<int2, int> ExitPortalLookup;

        public GraphSector (int index, CellRect bounds) {
            Index = index;
            Bounds = bounds;

            RootPortals = new NativeList<Portal>(Constants.EXPECTED_MAX_COLORS, Allocator.Persistent);
            ExitPortals = new NativeList<Portal>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
            ExitPortalLookup = new NativeHashMap<int2, int>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
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


        public void AddExitPortal (Portal portal) {
            ExitPortalLookup.Add(portal.Position.Cell, ExitPortals.Length);
            ExitPortals.Add(portal);
        }

        public void CreateRootPortals (ColorField colors) {

            // Color the edge portals
            for (int i = 0; i < ExitPortals.Length; i++) {
                var portal = ExitPortals[i];
                var tile = portal.Position.Cell - Bounds.MinCell;
                var color = colors.GetColor(tile);
                portal.Color = color;
                ExitPortals[i] = portal;
            }

            // Create the color roots
            for (int color = 1; color <= colors.NumColors; color++) {
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