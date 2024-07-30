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

        public PortalGraphSector[] sectors;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PortalGraph(Map map, int resolution) {

            this.resolution = resolution;
            width = map.Width;
            height = map.Height;

            widthSectors = Mathf.CeilToInt((float)map.Width / resolution);
            heightSectors = Mathf.CeilToInt((float)map.Height / resolution);
            sectors = new PortalGraphSector[widthSectors * heightSectors];

            //Set number of sectors in horizontal and vertical direction
            BuildSectors(resolution, widthSectors, heightSectors);
            InitialiseSectors(map);
            LinkSectors();
        }

        /// <summary>
        /// Create the node-based representation of the map
        /// </summary>
        private void InitialiseSectors(Map map) {
            for (int s = 0; s < sectors.Length; s++) {
                sectors[s].Build(map);
            }
        }

        public PortalGraphSector GetSector(int x, int y) {
            var sectorX = x / resolution;
            var sectorY = y / resolution;
            var index = sectorX + widthSectors * sectorY;
            return sectors[math.clamp(index, 0, sectors.Length - 1)];
        }

        public bool TryGetSectorRoot(int x, int y, out Portal node) {
            node = null;

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
            var color = sector.Colors.Colors[tileX, tileY];
            if (color <= 0) {
                return false;
            }

            // Return root node for color
            node = sector.RootPortals[color - 1];
            return true;

        }

        /// <summary>
        /// Connect two nodes by pathfinding between them. 
        /// </summary>
        /// <remarks>We assume they are different nodes. If the path returned is 0, then there is no path that connects them.</remarks>
        private bool ConnectPortals(Portal n1, Portal n2, PortalGraphSector c) {
            LinkedListNode<PortalEdge> iter;
            PortalEdge e1, e2;

            var corner = c.Boundaries.Min;
            var pathCost = SectorPathfinder.FindTravelCost(
                c.Costs, n1.pos - corner, n2.pos - corner);

            if (pathCost >= 0) {
                e1 = new PortalEdge() {
                    start = n1,
                    end = n2,
                    type = PortalEdgeType.INTRA,
                    weight = pathCost,
                };

                e2 = new PortalEdge() {
                    start = n2,
                    end = n1,
                    type = PortalEdgeType.INTRA,
                    weight = pathCost,
                };

                n1.edges.Add(e1);
                n2.edges.Add(e2);

                return true;
            }
            else {
                //No path, return false
                return false;
            }
        }

        private delegate void CreateBorderPortals(PortalGraphSector c1, PortalGraphSector c2, bool x);

        /// <summary>
        /// Build all graph sectors
        /// </summary>
        private void BuildSectors(int resolution, int width, int height) {
            PortalGraphSector sector;
            int x, y;

            //Create sectors of this level
            for (x = 0; x < width; ++x) {
                for (y = 0; y < height; ++y) {

                    var min = new int2(x * resolution, y * resolution);
                    var max = new int2(
                        Mathf.Min(min.x + resolution - 1, this.width - 1),
                        Mathf.Min(min.y + resolution - 1, this.height - 1));
                    var boundaries = new Boundaries { Min = min, Max = max };

                    sector = new PortalGraphSector(boundaries);
                    sectors[x + y * width] = sector;
                }
            }
        }

        /// <summary>
        /// Link all graph sectors
        /// </summary>
        private void LinkSectors() {

            // TODO: improve linking method

            //Add border nodes for every adjacent pair of sectors
            for (int i = 0; i < sectors.Length; i++) {
                for (int j = i + 1; j < sectors.Length; ++j) {
                    LinkAdjacentSectors(sectors[i], sectors[j]);
                }
            }

            //Add Intra edges for every border nodes and pathfind between them
            for (int i = 0; i < sectors.Length; ++i) {
                GenerateIntraEdges(sectors[i]);
            }

            // Create root nodes allowing each start tile to reach start portals
            for (int s = 0; s < sectors.Length; s++) {
                sectors[s].CreateRootPortals();
            }

        }

        private void LinkAdjacentSectors(PortalGraphSector c1, PortalGraphSector c2) {
            //Check if both sectors are adjacent
            if (c1.Boundaries.Min.x == c2.Boundaries.Min.x) {
                if (c1.Boundaries.Max.y + 1 == c2.Boundaries.Min.y)
                    CreateConcreteBorderPortals(c1, c2, false);
                else if (c2.Boundaries.Max.y + 1 == c1.Boundaries.Min.y)
                    CreateConcreteBorderPortals(c2, c1, false);

            }
            else if (c1.Boundaries.Min.y == c2.Boundaries.Min.y) {
                if (c1.Boundaries.Max.x + 1 == c2.Boundaries.Min.x)
                    CreateConcreteBorderPortals(c1, c2, true);
                else if (c2.Boundaries.Max.x + 1 == c1.Boundaries.Min.x)
                    CreateConcreteBorderPortals(c2, c1, true);
            }
        }

        /// <summary>
        /// Create border nodes and attach them together.
        /// We always pass the lower sector first (in c1).
        /// Adjacent index : if x == true, then c1.BottomRight.x else c1.BottomRight.y
        /// </summary>
        private void CreateConcreteBorderPortals(PortalGraphSector c1, PortalGraphSector c2, bool x) {
            int i, iMin, iMax;
            if (x) {
                iMin = c1.Boundaries.Min.y;
                iMax = iMin + c1.Size.y;
            }
            else {
                iMin = c1.Boundaries.Min.x;
                iMax = iMin + c1.Size.x;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (x && c1.IsOpenAt(new int2(c1.Boundaries.Max.x, i)) 
                        && c2.IsOpenAt(new int2(c2.Boundaries.Min.x, i))) {
                    lineSize++;
                }
                else if (!x && c1.IsOpenAt(new int2(i, c1.Boundaries.Max.y)) 
                        && c2.IsOpenAt(new int2(i, c2.Boundaries.Min.y))) {
                    lineSize++;
                }
                else {
                    CreateInterEdges(c1, c2, x, ref lineSize, i);
                }
                /*if (x && (portals.ContainsKey(new int2(c1.Boundaries.Max.x, i)) && portals.ContainsKey(new int2(c2.Boundaries.Min.x, i)))
                    || !x && (portals.ContainsKey(new int2(i, c1.Boundaries.Max.y)) && portals.ContainsKey(new int2(i, c2.Boundaries.Min.y)))) {
                    lineSize++;
                }*/
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateInterEdges(c1, c2, x, ref lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateInterEdges(PortalGraphSector c1, PortalGraphSector c2, bool x, ref int lineSize, int i) {
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
        private void CreateInterEdge(PortalGraphSector c1, PortalGraphSector c2, bool x, int i) {
            int2 g1, g2;
            Portal n1, n2;
            if (x) {
                g1 = new int2(c1.Boundaries.Max.x, i);
                g2 = new int2(c2.Boundaries.Min.x, i);
            }
            else {
                g1 = new int2(i, c1.Boundaries.Max.y);
                g2 = new int2(i, c2.Boundaries.Min.y);
            }

            if (!c1.Portals.TryGetValue(g1, out n1)) {
                n1 = new Portal(g1);
                c1.Portals.Add(g1, n1);
                //n1.child = portals[g1];
            }

            if (!c2.Portals.TryGetValue(g2, out n2)) {
                n2 = new Portal(g2);
                c2.Portals.Add(g2, n2);
                //n2.child = portals[g2];
            }

            n1.edges.Add(new PortalEdge() { start = n1, end = n2, type = PortalEdgeType.INTER, weight = 1 });
            n2.edges.Add(new PortalEdge() { start = n2, end = n1, type = PortalEdgeType.INTER, weight = 1 });
        }

        //Intra edges are edges that lives inside sectors
        private void GenerateIntraEdges(PortalGraphSector c) {
            int i, j;
            Portal n1, n2;

            /* We do this so that we can iterate through pairs once, 
             * by keeping the second index always higher than the first */
            var nodes = new List<Portal>(c.Portals.Values);

            for (i = 0; i < nodes.Count; ++i) {
                n1 = nodes[i];
                for (j = i + 1; j < nodes.Count; ++j) {
                    n2 = nodes[j];

                    ConnectPortals(n1, n2, c);
                }
            }
        }

    }

}