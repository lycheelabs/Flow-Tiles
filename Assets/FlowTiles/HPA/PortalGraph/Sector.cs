using System.Collections.Generic;
using System.Drawing;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public class Sector {

        //Boundaries of the cluster (with respect to the original map)
        public Boundaries Boundaries;
        public Dictionary<int2, Portal> EdgePortals;
        public List<Portal> RootPortals;

        public int Index;
        public int2 Size;
        public CostField Costs;
        public ColorField Colors;

        public int2 CenterTile => new int2(Size.x / 2, Size.y / 2) + Boundaries.Min;

        public Sector (int index, Boundaries boundaries) {
            Index = index;
            Boundaries = new Boundaries();
            EdgePortals = new Dictionary<int2, Portal>();
            RootPortals = new List<Portal>();

            Boundaries = boundaries;
            Size = Boundaries.Max - Boundaries.Min + 1;

            Costs = new CostField(Size);
            Colors = new ColorField(Size);
        }

        public void Build(Map map) {
            Costs.Initialise(map, Boundaries.Min);
            Colors.Recolor(Costs);
        }

        public bool Contains(int2 pos) {
            return pos.x >= Boundaries.Min.x &&
                pos.x <= Boundaries.Max.x &&
                pos.y >= Boundaries.Min.y &&
                pos.y <= Boundaries.Max.y;
        }

        public bool IsOpenAt(int2 pos) {
            if (Contains(pos) 
                && Costs.GetCost(pos - Boundaries.Min) != CostField.WALL) {
                return true;
            }
            return false;
        }

        public void CreateRootPortals () {

            // Wasteful...
            var nodes = new List<Portal>(EdgePortals.Values);

            for (int i = 0; i < nodes.Count; i++) {
                var portal = nodes[i];
                var tile = portal.Position.Cell - Boundaries.Min;
                var color = Colors.GetColor(tile);
                portal.Color = color;
                EdgePortals[portal.Position.Cell] = portal;
            }

            // Wasteful...
            nodes = new List<Portal>(EdgePortals.Values);

            for (int color = 1; color <= Colors.NumColors; color++) {
                var colorPortal = new Portal(CenterTile, Index, 0);
                colorPortal.Color = color;

                for (int p = 0; p < nodes.Count; p++) {
                    var portal = nodes[p];
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