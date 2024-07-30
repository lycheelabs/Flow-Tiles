using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct CostField {

        public const byte WALL = 255;
        public const byte OPEN = 1;

        public int2 size;
        public byte[,] Costs;

        public CostField(int2 size) {
            this.size = size;
            Costs = new byte[size.x, size.y];
        }

        public void Initialise(Map map, int2 corner) {
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var mapCell = corner + new int2(x, y);
                    var blocked = map.Obstacles[mapCell.y][mapCell.x];

                    byte cost = OPEN;
                    if (blocked) cost = WALL;
                    Costs[x,y] = cost;
                }
            }
        }

    }

    public struct ColorField {

        public int2 size;
        public short[,] Colors;
        public short NumColors;

        public ColorField(int2 size) {
            this.size = size;
            Colors = new short[size.x, size.y];
            NumColors = 0;
        }

        public void Recolor (CostField costs) {
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    Colors[x, y] = 0;

                    var cost = costs.Costs[x, y];
                    var blocked = cost == CostField.WALL;
                    if (blocked) Colors[x, y] = -1;
                }
            }

            NumColors = 0;
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    if (Colors[x, y] == 0) {
                        NumColors++;
                        FloodFill(new int2(x, y), NumColors);
                    }
                }
            }

        }

        private void FloodFill(int2 startPoint, short newColorIndex) {
            Stack<int2> points = new Stack<int2>();
            points.Push(startPoint);

            while (points.Count != 0) {
                int2 temp = points.Pop();
                int y1 = temp.y;
                while (y1 >= 0 && Colors[temp.x, y1] == 0) {
                    y1--;
                }
                y1++;
                bool spanLeft = false;
                bool spanRight = false;

                while (y1 < size.y && Colors[temp.x, y1] == 0) {
                    Colors[temp.x, y1] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && Colors[temp.x - 1, y1] == 0) {
                        points.Push(new int2(temp.x - 1, y1));
                        spanLeft = true;
                    }
                    else if (spanLeft && temp.x - 1 == 0 && Colors[temp.x - 1, y1] != 0) {
                        spanLeft = false;
                    }
                    if (!spanRight && temp.x < size.x - 1 && Colors[temp.x + 1, y1] == 0) {
                        points.Push(new int2(temp.x + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && temp.x < size.x - 1 && Colors[temp.x + 1, y1] != 0) {
                        spanRight = false;
                    }
                    y1++;
                }
            }
        }

    }

    /// <summary>
    /// Domain-independent, rectangular clusters
    /// </summary>
    public class PortalGraphSector {

        //Boundaries of the cluster (with respect to the original map)
        public Boundaries Boundaries;
        public Dictionary<int2, Portal> Portals;
        public List<Portal> RootPortals;

        public int2 Size;
        public CostField Costs;
        public ColorField Colors;

        public PortalGraphSector(Boundaries boundaries) {
            Boundaries = new Boundaries();
            Portals = new Dictionary<int2, Portal>();
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
                && Costs.Costs[pos.x - Boundaries.Min.x, pos.y - Boundaries.Min.y] != CostField.WALL) {
                return true;
            }
            return false;
        }

        public void CreateRootPortals () {
            var nodes = new List<Portal>(Portals.Values);

            for (int i = 0; i < nodes.Count; i++) {
                var portal = nodes[i];
                var tile = portal.pos - Boundaries.Min;
                var color = Colors.Colors[tile.x, tile.y];
                portal.color = color;
            }

            var centerTile = new int2(Size.x / 2, Size.y / 2) + Boundaries.Min;
            for (int c = 1; c <= Colors.NumColors; c++) {
                var colorPortal = new Portal(centerTile);
                colorPortal.color = c;

                for (int p = 0; p < nodes.Count; p++) {
                    var portal = nodes[p];
                    if (portal.color == c) {
                        colorPortal.edges.Add(new PortalEdge {
                            start = colorPortal,
                            end = portal,
                            type = PortalEdgeType.INTRA,
                            weight = 0,
                        });
                        portal.root = colorPortal;
                    }
                }

                RootPortals.Add(colorPortal);
            }

        }

    }

}