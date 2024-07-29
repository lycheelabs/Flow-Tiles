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

        public Dictionary<int2, Portal> portals;
        public PortalGraphSector[] sectors;

        //We keep track of added nodes to remove them afterwards
        List<Portal> AddedPortals;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PortalGraph(Map map, int resolution) {
            AddedPortals = new List<Portal>();

            this.resolution = resolution;
            width = map.Width;
            height = map.Height;

            widthSectors = Mathf.CeilToInt((float)map.Width / resolution);
            heightSectors = Mathf.CeilToInt((float)map.Height / resolution);
            sectors = new PortalGraphSector[widthSectors * heightSectors];

            //Set number of sectors in horizontal and vertical direction
            BuildSectors(resolution, widthSectors, heightSectors);

            portals = CreateMapRepresentation(map);
            LinkSectors();
        }

        public PortalGraphSector GetSector(int x, int y) {
            var sectorX = x / resolution;
            var sectorY = y / resolution;
            var index = sectorX + widthSectors * sectorY;
            return sectors[math.clamp(index, 0, sectors.Length - 1)];
        }

        /// <summary>
        /// Create the node-based representation of the map
        /// </summary>
        private Dictionary<int2, Portal> CreateMapRepresentation(Map map) {
            var mapnodes = new Dictionary<int2, Portal>(map.FreeTiles);
            int i, j;
            int2 gridTile;

            // Colorise the sectors...
            for (int s = 0; s < sectors.Length; s++) {
                sectors[s].FindColors(map);
            }

            //1. Create all nodes necessary
            for (i = 0; i < map.Width; ++i) {
                for (j = 0; j < map.Height; ++j) {
                    if (!map.Obstacles[j][i]) {
                        gridTile = new int2(i, j);
                        mapnodes.Add(gridTile, new Portal(gridTile));
                    }
                }
            }

            //2. Create all possible edges
            foreach (Portal n in mapnodes.Values) {

                //Look for straight edges
                for (i = -1; i < 2; i += 2) {
                    SearchMapEdge(map, mapnodes, n, n.pos.x + i, n.pos.y, false);
                    SearchMapEdge(map, mapnodes, n, n.pos.x, n.pos.y + i, false);
                }

                //Look for diagonal edges
                for (i = -1; i < 2; i += 2) {
                    for (j = -1; j < 2; j += 2) {
                        SearchMapEdge(map, mapnodes, n, n.pos.x + i, n.pos.y + j, true);
                    }
                }
            }

            return mapnodes;
        }

        /// <summary>
        /// Add the edge to the node if it's a valid map edge
        /// </summary>
        private void SearchMapEdge(Map map, Dictionary<int2, Portal> mapPortals, Portal n, int x, int y, bool diagonal) {
            var weight = diagonal ? SQRT2 : 1f;
            int2 gridTile = 0;

            //Don't let diagonal movement occur when an obstacle is crossing the edge
            if (diagonal) {
                gridTile.x = n.pos.x;
                gridTile.y = y;
                if (!map.IsFreeTile(gridTile)) return;

                gridTile.x = x;
                gridTile.y = n.pos.y;
                if (!map.IsFreeTile(gridTile)) return;
            }

            gridTile.x = x;
            gridTile.y = y;
            if (!map.IsFreeTile(gridTile)) return;

            //Edge is valid, add it to the node
            n.edges.Add(new PortalEdge() {
                start = n,
                end = mapPortals[gridTile],
                type = PortalEdgeType.INTER,
                weight = weight
            });
        }

        /// <summary>
        /// Insert start and dest nodes in graph
        /// </summary>
        public void InsertPortals(int2 start, int2 dest, out Portal nStart, out Portal nDest) {
            PortalGraphSector cStart, cDest;
            Portal newStart, newDest;
            nStart = portals[start];
            nDest = portals[dest];
            bool isConnected;
            AddedPortals.Clear();

            cStart = null;
            cDest = null;
            isConnected = false;

            foreach (PortalGraphSector c in sectors) {
                if (c.Contains(start))
                    cStart = c;

                if (c.Contains(dest))
                    cDest = c;

                if (cStart != null && cDest != null)
                    break;
            }

            //This is the right sector
            if (cStart == cDest) {
                newStart = new Portal(start) { child = nStart };
                newDest = new Portal(dest) { child = nDest };

                isConnected = ConnectPortals(newStart, newDest, cStart);

                if (isConnected) {
                    //If they are reachable then we set them as the nodes
                    //Otherwise we might be able to reach them from an upper layer
                    nStart = newStart;
                    nDest = newDest;
                }
            }

            if (!isConnected) {
                nStart = ConnectToBorder(start, cStart, nStart);
                nDest = ConnectToBorder(dest, cDest, nDest);
            }

        }

        /// <summary>
        /// Remove nodes from the graph, including all underlying edges
        /// </summary>
        public void RemoveAddedPortals() {
            foreach (Portal n in AddedPortals)
                foreach (PortalEdge e in n.edges)
                    //Find an edge in current.end that points to this node
                    e.end.edges.RemoveAll((ee) => ee.end == n);
        }

        /// <summary>
        /// Connect the grid tile to borders by creating a new node
        /// </summary>
        /// <returns>The node created</returns>
        private Portal ConnectToBorder(int2 pos, PortalGraphSector c, Portal child) {
            Portal newPortal;

            //If the position is an actual border node, then return it
            if (c.Portals.TryGetValue(pos, out newPortal))
                return newPortal;

            //Otherwise create a node and pathfind through border nodes
            newPortal = new Portal(pos) { child = child };

            foreach (KeyValuePair<int2, Portal> n in c.Portals) {
                ConnectPortals(newPortal, n.Value, c);
            }

            //Since this node is not part of the graph, we keep track of it to remove it later
            AddedPortals.Add(newPortal);

            return newPortal;
        }

        /// <summary>
        /// Connect two nodes by pathfinding between them. 
        /// </summary>
        /// <remarks>We assume they are different nodes. If the path returned is 0, then there is no path that connects them.</remarks>
        private bool ConnectPortals(Portal n1, Portal n2, PortalGraphSector c) {
            LinkedList<PortalEdge> path;
            LinkedListNode<PortalEdge> iter;
            PortalEdge e1, e2;

            float weight = 0f;

            path = AstarPathfinder.FindPath(n1.child, n2.child, c.Boundaries);

            if (path.Count > 0) {
                e1 = new PortalEdge() {
                    start = n1,
                    end = n2,
                    type = PortalEdgeType.INTRA,
                    UnderlyingPath = path
                };

                e2 = new PortalEdge() {
                    start = n2,
                    end = n1,
                    type = PortalEdgeType.INTRA,
                    UnderlyingPath = new LinkedList<PortalEdge>()
                };

                //Store inverse path in node n2
                //Sum weights of underlying edges at the same time
                iter = e1.UnderlyingPath.Last;
                while (iter != null) {
                    // Find twin edge
                    var val = iter.Value.end.edges.Find(
                        e => e.start == iter.Value.end && e.end == iter.Value.start);

                    e2.UnderlyingPath.AddLast(val);
                    weight += val.weight;
                    iter = iter.Previous;
                }

                //Update weights
                e1.weight = weight;
                e2.weight = weight;

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
                    DetectAdjacentSectors(sectors[i], sectors[j]);
                }
            }

            //Add Intra edges for every border nodes and pathfind between them
            for (int i = 0; i < sectors.Length; ++i) {
                GenerateIntraEdges(sectors[i]);
            }
        }

        private void DetectAdjacentSectors(PortalGraphSector c1, PortalGraphSector c2) {
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
                iMax = iMin + c1.Height;
            }
            else {
                iMin = c1.Boundaries.Min.x;
                iMax = iMin + c1.Width;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (x && (portals.ContainsKey(new int2(c1.Boundaries.Max.x, i)) && portals.ContainsKey(new int2(c2.Boundaries.Min.x, i)))
                    || !x && (portals.ContainsKey(new int2(i, c1.Boundaries.Max.y)) && portals.ContainsKey(new int2(i, c2.Boundaries.Min.y)))) {
                    lineSize++;
                }
                else {

                    CreateConcreteInterEdges(c1, c2, x, ref lineSize, i);
                }
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateConcreteInterEdges(c1, c2, x, ref lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateConcreteInterEdges(PortalGraphSector c1, PortalGraphSector c2, bool x, ref int lineSize, int i) {
            if (lineSize > 0) {
                if (lineSize <= 5 || true) {
                    //Line is too small, create 1 inter edges
                    CreateConcreteInterEdge(c1, c2, x, i - (lineSize / 2 + 1));
                }
                else {
                    //Create 2 inter edges
                    CreateConcreteInterEdge(c1, c2, x, i - lineSize);
                    CreateConcreteInterEdge(c1, c2, x, i - 1);
                }

                lineSize = 0;
            }
        }

        //Inter edges are edges that crosses sectors
        private void CreateConcreteInterEdge(PortalGraphSector c1, PortalGraphSector c2, bool x, int i) {
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
                n1.child = portals[g1];
            }

            if (!c2.Portals.TryGetValue(g2, out n2)) {
                n2 = new Portal(g2);
                c2.Portals.Add(g2, n2);
                n2.child = portals[g2];
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