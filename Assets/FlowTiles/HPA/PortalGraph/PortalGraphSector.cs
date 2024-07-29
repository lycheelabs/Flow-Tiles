using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalGraphs {

    /// <summary>
    /// Domain-independent, rectangular clusters
    /// </summary>
    public class PortalGraphSector {

        //Boundaries of the cluster (with respect to the original map)
        public Boundaries Boundaries;
        public Dictionary<int2, Portal> Portals;

        public int NumColors;
        public int[,] Colors;
        public List<Portal> RootPortals;

        public int Width;
        public int Height;

        public PortalGraphSector(Boundaries boundaries) {
            Boundaries = new Boundaries();
            Portals = new Dictionary<int2, Portal>();
            
            Boundaries = boundaries;
            Width = Boundaries.Max.x - Boundaries.Min.x + 1;
            Height = Boundaries.Max.y - Boundaries.Min.y + 1;
            Colors = new int[Width, Height];
            RootPortals = new List<Portal>();
        }

        public bool Contains(int2 pos) {
            return pos.x >= Boundaries.Min.x &&
                pos.x <= Boundaries.Max.x &&
                pos.y >= Boundaries.Min.y &&
                pos.y <= Boundaries.Max.y;
        }

        public void FindColors(Map map) {
            var corner = Boundaries.Min;

            for (int x = 0; x < Width; x++) {
                for (var y = 0; y < Height; y++) {
                    var mapCell = corner + new int2(x, y);
                    var blocked = map.Obstacles[mapCell.y][mapCell.x];
                    Colors[x, y] = blocked ? -1 : 0;
                }
            }

            NumColors = 0;
            for (int x = 0; x < Width; x++) {
                for (var y = 0; y < Height; y++) {
                    if (map.Obstacles[y + corner.y][x + corner.x]) continue;

                    if (Colors[x, y] == 0) {
                        NumColors++;
                        FloodFill(new int2(x, y), NumColors);
                    }
                }
            }

        }

        public void CreateRootPortals () {
            var nodes = new List<Portal>(Portals.Values);

            for (int i = 0; i < nodes.Count; i++) {
                var portal = nodes[i];
                var tile = portal.pos - Boundaries.Min;
                var color = Colors[tile.x, tile.y];
                portal.color = color;
            }

            var centerTile = new int2(Width / 2, Height / 2) + Boundaries.Min;
            for (int c = 1; c <= NumColors; c++) {
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

        private void FloodFill(int2 startPoint, int newColorIndex) {
            Stack<int2> points = new Stack<int2>();
            //Colors[startPoint.x, startPoint.y] = newColorIndex;

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

                while (y1 < Height && Colors[temp.x, y1] == 0) {
                    Colors[temp.x, y1] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && Colors[temp.x - 1, y1] == 0) {
                        points.Push(new int2(temp.x - 1, y1));
                        spanLeft = true;
                    }
                    else if (spanLeft && temp.x - 1 == 0 && Colors[temp.x - 1, y1] != 0) {
                        spanLeft = false;
                    }
                    if (!spanRight && temp.x < Width - 1 && Colors[temp.x + 1, y1] == 0) {
                        points.Push(new int2(temp.x + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && temp.x < Width - 1 && Colors[temp.x + 1, y1] != 0) {
                        spanRight = false;
                    }
                    y1++;
                }

            }

        }

        /*
        private void FloodFill(Bitmap bmp, Point pt, Color targetColor, Color replacementColor)
        {
            targetColor = bmp.GetPixel(pt.X, pt.Y);
            if (targetColor.ToArgb().Equals(replacementColor.ToArgb()))
            {
                return;
            }
 
            Stack<Point> pixels = new Stack<Point>();
             
            pixels.Push(pt);
            while (pixels.Count != 0)
            {
                Point temp = pixels.Pop();
                int y1 = temp.Y;
                while (y1 >= 0 && bmp.GetPixel(temp.X, y1) == targetColor)
                {
                    y1--;
                }
                y1++;
                bool spanLeft = false;
                bool spanRight = false;
                while (y1 < bmp.Height && bmp.GetPixel(temp.X, y1) == targetColor)
                {
                    bmp.SetPixel(temp.X, y1, replacementColor);
 
                    if (!spanLeft && temp.X > 0 && bmp.GetPixel(temp.X - 1, y1) == targetColor)
                    {
                        pixels.Push(new Point(temp.X - 1, y1));
                        spanLeft = true;
                    }
                    else if(spanLeft && temp.X - 1 == 0 && bmp.GetPixel(temp.X - 1, y1) != targetColor)
                    {
                        spanLeft = false;
                    }
                    if (!spanRight && temp.X < bmp.Width - 1 && bmp.GetPixel(temp.X + 1, y1) == targetColor)
                    {
                        pixels.Push(new Point(temp.X + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && temp.X < bmp.Width - 1 && bmp.GetPixel(temp.X + 1, y1) != targetColor)
                    {
                        spanRight = false;
                    } 
                    y1++;
                }
 
            }
            pictureBox1.Refresh();
                    
        }
         */

    }

}