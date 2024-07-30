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
        /// We always pass the lower sector first (in sector1).
        /// </summary>
        private void LinkAdjacentSectors(Sector sector1, Sector sector2, bool horizontal) {
            int i, iMin, iMax;
            if (horizontal) {
                iMin = sector1.Boundaries.Min.y;
                iMax = iMin + sector1.Size.y;
            }
            else {
                iMin = sector1.Boundaries.Min.x;
                iMax = iMin + sector1.Size.x;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (horizontal && sector1.IsOpenAt(new int2(sector1.Boundaries.Max.x, i)) 
                        && sector2.IsOpenAt(new int2(sector2.Boundaries.Min.x, i))) {
                    lineSize++;
                }
                else if (!horizontal && sector1.IsOpenAt(new int2(i, sector1.Boundaries.Max.y)) 
                        && sector2.IsOpenAt(new int2(i, sector2.Boundaries.Min.y))) {
                    lineSize++;
                }
                else {
                    CreateInterEdges(sector1, sector2, horizontal, lineSize, i);
                    lineSize = 0;
                }
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateInterEdges(sector1, sector2, horizontal, lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateInterEdges(Sector sector1, Sector sector2, bool horizontal, int lineSize, int i) {
            if (lineSize > 0) {
                CreateInterEdge(sector1, sector2, horizontal, i - lineSize, i - 1);// i - (lineSize / 2 + 1));
            }
        }

        //Inter edges are edges that crosses sectors
        private void CreateInterEdge(Sector sector1, Sector sector2, bool horizontal, int start, int end) {
            int mid = (start + end) / 2;
            int2 start1, start2, end1, end2, dir;
            Portal portal1, portal2;
            if (horizontal) {
                start1 = new int2(sector1.Boundaries.Max.x, start);
                end1 = new int2(sector1.Boundaries.Max.x, end);
                start2 = new int2(sector2.Boundaries.Min.x, start);
                end2 = new int2(sector2.Boundaries.Min.x, end);
                dir = new int2(1, 0);
            }
            else {
                start1 = new int2(start, sector1.Boundaries.Max.y);
                end1 = new int2(end, sector1.Boundaries.Max.y);
                start2 = new int2(start, sector2.Boundaries.Min.y);
                end2 = new int2(end, sector2.Boundaries.Min.y);
                dir = new int2(0, 1);
            }

            var mid1 = (start1 + end1) / 2;
            if (!sector1.EdgePortals.TryGetValue(mid1, out portal1)) {
                portal1 = new Portal(start1, end1, sector1.Index, dir);
                sector1.EdgePortals.Add(mid1, portal1);
            }

            var mid2 = (start2 + end2) / 2;
            if (!sector2.EdgePortals.TryGetValue(mid2, out portal2)) {
                portal2 = new Portal(start2, end2, sector2.Index, -dir);
                sector2.EdgePortals.Add(mid2, portal2);
            }

            portal1.Edges.Add(new PortalEdge() {
                start = portal1.Position,
                end = portal2.Position,
                weight = 1 
            });
            portal2.Edges.Add(new PortalEdge() {
                start = portal2.Position,
                end = portal1.Position,
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