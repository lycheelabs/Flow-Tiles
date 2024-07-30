using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalGraphs {

    public class PortalGraph {

        public static float SQRT2 = Mathf.Sqrt(2f);

        readonly int resolution;
        readonly int width;
        readonly int height;
        readonly int widthSectors;
        readonly int heightSectors;

        public Sector[] sectors;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PortalGraph(Map map, int resolution) {

            this.resolution = resolution;
            width = map.Width;
            height = map.Height;

            widthSectors = Mathf.CeilToInt((float)map.Width / resolution);
            heightSectors = Mathf.CeilToInt((float)map.Height / resolution);
            sectors = new Sector[widthSectors * heightSectors];

            //Set number of sectors in horizontal and vertical direction
            BuildSectors(resolution, widthSectors, heightSectors);
            InitialiseSectors(map);
            LinkSectors();
        }

        public Sector GetSector(int x, int y) {
            var sectorX = x / resolution;
            var sectorY = y / resolution;
            var index = sectorX + widthSectors * sectorY;
            return sectors[math.clamp(index, 0, sectors.Length - 1)];
        }

        public bool TryGetSectorRoot(int x, int y, out Portal node) {
            node = default;

            // Find sector
            var sectorX = x / resolution;
            var sectorY = y / resolution;
            var index = sectorX + widthSectors * sectorY;
            if (index < 0 || index >= sectors.Length) {
                return false;
            }

            // Find color
            var sector = sectors[index];
            var tileX = x % resolution;
            var tileY = y % resolution;
            var color = sector.Colors.GetColor(tileX, tileY);
            if (color <= 0) {
                return false;
            }

            // Return root node for color
            node = sector.RootPortals[color - 1];
            return true;

        }

        /// <summary>
        /// Build all graph sectors
        /// </summary>
        private void BuildSectors(int resolution, int width, int height) {
            Sector sector;
            int x, y;

            //Create sectors of this level
            for (x = 0; x < width; ++x) {
                for (y = 0; y < height; ++y) {

                    var min = new int2(x * resolution, y * resolution);
                    var max = new int2(
                        Mathf.Min(min.x + resolution - 1, this.width - 1),
                        Mathf.Min(min.y + resolution - 1, this.height - 1));
                    var boundaries = new Boundaries { Min = min, Max = max };

                    var index = x + y * width;
                    sector = new Sector(index, boundaries);
                    sectors[x + y * width] = sector;
                }
            }
        }

        /// <summary>
        /// Cost and color each sector
        /// </summary>
        private void InitialiseSectors(Map map) {
            for (int s = 0; s < sectors.Length; s++) {
                sectors[s].Build(map);
            }
        }

        /// <summary>
        /// Link all graph sectors
        /// </summary>
        private void LinkSectors() {

            //Add border nodes for every adjacent pair of sectors
            for (int i = 0; i < sectors.Length; i++) {

                var x = i % widthSectors;
                if (x < widthSectors - 1) {
                    LinkAdjacentSectors(sectors[i], sectors[i + 1], true);
                }

                var y = i / widthSectors;
                if (y < heightSectors - 1) {
                    LinkAdjacentSectors(sectors[i], sectors[i + widthSectors], false);
                }

            }

            //Add Intra edges for every border nodes and pathfind between them
            for (int i = 0; i < sectors.Length; ++i) {
                GenerateIntraEdges(sectors[i]);
            }

            // Create root portals allowing each start tile to reach the same-colored edge portals
            for (int s = 0; s < sectors.Length; s++) {
                sectors[s].CreateRootPortals();
            }

        }

        /// <summary>
        /// Create border nodes and attach them together.
        /// We always pass the lower sector first (in c1).
        /// </summary>
        private void LinkAdjacentSectors(Sector c1, Sector c2, bool horizontal) {
            int i, iMin, iMax;
            if (horizontal) {
                iMin = c1.Boundaries.Min.y;
                iMax = iMin + c1.Size.y;
            }
            else {
                iMin = c1.Boundaries.Min.x;
                iMax = iMin + c1.Size.x;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (horizontal && c1.IsOpenAt(new int2(c1.Boundaries.Max.x, i)) 
                        && c2.IsOpenAt(new int2(c2.Boundaries.Min.x, i))) {
                    lineSize++;
                }
                else if (!horizontal && c1.IsOpenAt(new int2(i, c1.Boundaries.Max.y)) 
                        && c2.IsOpenAt(new int2(i, c2.Boundaries.Min.y))) {
                    lineSize++;
                }
                else {
                    CreateInterEdges(c1, c2, horizontal, ref lineSize, i);
                }
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateInterEdges(c1, c2, horizontal, ref lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateInterEdges(Sector c1, Sector c2, bool x, ref int lineSize, int i) {
            if (lineSize > 0) {
                if (lineSize <= 5 || true) {
                    //Line is too small, create 1 inter edges
                    CreateInterEdge(c1, c2, x, i - (lineSize / 2 + 1));
                }
                else {
                    //Create 2 inter edges
                    CreateInterEdge(c1, c2, x, i - lineSize);
                    CreateInterEdge(c1, c2, x, i - 1);
                }

                lineSize = 0;
            }
        }

        //Inter edges are edges that crosses sectors
        private void CreateInterEdge(Sector c1, Sector c2, bool x, int i) {
            int2 g1, g2, dir;
            Portal n1, n2;
            if (x) {
                g1 = new int2(c1.Boundaries.Max.x, i);
                g2 = new int2(c2.Boundaries.Min.x, i);
                dir = new int2(1, 0);
            }
            else {
                g1 = new int2(i, c1.Boundaries.Max.y);
                g2 = new int2(i, c2.Boundaries.Min.y);
                dir = new int2(0, 1);
            }

            if (!c1.EdgePortals.TryGetValue(g1, out n1)) {
                n1 = new Portal(g1, c1.Index, dir);
                c1.EdgePortals.Add(g1, n1);
            }

            if (!c2.EdgePortals.TryGetValue(g2, out n2)) {
                n2 = new Portal(g2, c2.Index, -dir);
                c2.EdgePortals.Add(g2, n2);
            }

            n1.Edges.Add(new PortalEdge() {
                start = n1.Position,
                end = n2.Position,
                weight = 1 
            });
            n2.Edges.Add(new PortalEdge() {
                start = n2.Position,
                end = n1.Position,
                weight = 1 
            });
        }

        //Intra edges are edges that lives inside sectors
        private void GenerateIntraEdges(Sector c) {
            int i, j;
            Portal n1, n2;

            // We do this so that we can iterate through pairs once, 
            // by keeping the second index always higher than the first.
            // For a path to exit, both portals must have matching color.        
            var nodes = new List<Portal>(c.EdgePortals.Values);
            for (i = 0; i < nodes.Count; ++i) {
                n1 = nodes[i];
                for (j = i + 1; j < nodes.Count; ++j) {
                    n2 = nodes[j];
                    if (n1.Color == n2.Color) {
                        TryConnectPortals(n1, n2, c);
                    }
                }
            }
        }

        /// <summary>
        /// Connect two nodes by pathfinding between them. 
        /// </summary>
        /// <remarks>We assume they are different nodes. If the path returned is 0, then there is no path that connects them.</remarks>
        private bool TryConnectPortals(Portal n1, Portal n2, Sector c) {
            PortalEdge e1, e2;

            var corner = c.Boundaries.Min;
            var pathCost = SectorPathfinder.FindTravelCost(
                c.Costs, n1.Position.Cell - corner, n2.Position.Cell - corner);

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