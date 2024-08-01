using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace FlowTiles.PortalGraphs {

    [BurstCompile]
    public struct PortalGraph {

        [BurstCompile]
        public static void BurstBuild (ref PortalGraph graph, ref PathableMap map) {
            graph.Build (map);
        }
        
        // -----------------------------------------

        public readonly int2 sizeCells;
        public readonly int2 sizeSectors;
        public readonly int resolution;

        public NativeArray<MapSector> MapSectors;
        public NativeArray<GraphSector> GraphSectors;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PortalGraph(int2 sizeCells, int resolution) {
            this.sizeCells = sizeCells;
            this.resolution = resolution;

            var sectorsW = Mathf.CeilToInt((float)sizeCells.x / resolution);
            var sectorsH = Mathf.CeilToInt((float)sizeCells.y / resolution);
            sizeSectors = new int2(sectorsW, sectorsH);
            MapSectors = new NativeArray<MapSector>(sectorsW * sectorsH, Allocator.Persistent);
            GraphSectors = new NativeArray<GraphSector>(sectorsW * sectorsH, Allocator.Persistent);
        }

        public MapSector GetMapSector(int cellX, int cellY) {
            var sectorX = cellX / resolution;
            var sectorY = cellY / resolution;
            var index = sectorX + sizeSectors.x * sectorY;
            return MapSectors[index];
        }

        public GraphSector GetGraphSector(int cellX, int cellY) {
            var sectorX = cellX / resolution;
            var sectorY = cellY / resolution;
            var index = sectorX + sizeSectors.x * sectorY;
            return GraphSectors[index];
        }

        public int GetColor(int cellX, int cellY) {
            var mapSector = GetMapSector(cellX, cellY);
            var tileX = cellX % resolution;
            var tileY = cellY % resolution;
            return mapSector.Colors[tileX, tileY];
        }

        public bool TryGetClusterRoot(int cellX, int cellY, out Portal cluster) {
            
            // Find color
            var color = GetColor(cellX, cellY);
            if (color <= 0) {
                cluster = default;
                return false;
            }

            // Return root node for color
            var graphSector = GetGraphSector(cellX, cellY);
            cluster = graphSector.RootPortals[color - 1];
            return true;

        }

        public void Build(PathableMap map) {
            BuildSectors();
            InitialiseSectors(map);
            LinkSectors();
        }

        private void BuildSectors() {
            for (int x = 0; x < sizeSectors.x; x++) {
                for (int y = 0; y < sizeSectors.y; y++) {

                    var min = new int2(x * resolution, y * resolution);
                    var max = new int2(
                        Mathf.Min(min.x + resolution - 1, sizeCells.x - 1),
                        Mathf.Min(min.y + resolution - 1, sizeCells.y - 1));
                    var boundaries = new CellRect { MinCell = min, MaxCell = max };

                    var index = x + y * sizeSectors.x;
                    MapSectors[index] = new MapSector(boundaries);
                    GraphSectors[index] = new GraphSector(index, boundaries);
                }
            }
        }

        private void InitialiseSectors(PathableMap map) {
            for (int s = 0; s < MapSectors.Length; s++) {
                var sector = MapSectors[s];
                sector.Build(map);
                MapSectors[s] = sector;
            }
        }

        private void LinkSectors() {

            //Add border nodes for every adjacent pair of GraphSectors
            for (int i = 0; i < GraphSectors.Length; i++) {

                var x = i % sizeSectors.x;
                if (x < sizeSectors.x - 1) {
                    LinkAdjacentSectors(i, i + 1, true);
                }

                var y = i / sizeSectors.x;
                if (y < sizeSectors.y - 1) {
                    LinkAdjacentSectors(i, i + sizeSectors.x, false);
                }

            }

            //Add Intra edges for every border nodes and pathfind between them
            var pathfinder = new SectorPathfinder(resolution * resolution, Allocator.Temp);
            for (int s = 0; s < GraphSectors.Length; ++s) {
                var sector = GraphSectors[s];
                sector.ConnectExitPortals(MapSectors[s], pathfinder);
                GraphSectors[s] = sector;
            }

            // Create root portals allowing each start tile to reach the same-colored edge portals
            for (int s = 0; s < GraphSectors.Length; s++) {
                var sector = GraphSectors[s];
                sector.CreateRootPortals(MapSectors[s]);
                GraphSectors[s] = sector;
            }

        }

        /// <summary>
        /// Create border nodes and attach them together.
        /// We always pass the lower sector first (in sector1).
        /// </summary>
        private void LinkAdjacentSectors(int index1, int index2, bool horizontal) {
            var sector1 = MapSectors[index1];
            var sector2 = MapSectors[index2];

            int i, iMin, iMax;
            if (horizontal) {
                iMin = sector1.Bounds.MinCell.y;
                iMax = iMin + sector1.Bounds.HeightCells;
            }
            else {
                iMin = sector1.Bounds.MinCell.x;
                iMax = iMin + sector1.Bounds.WidthCells;
            }

            int lineSize = 0;
            for (i = iMin; i < iMax; ++i) {
                if (horizontal && sector1.IsOpenAt(new int2(sector1.Bounds.MaxCell.x, i)) 
                        && sector2.IsOpenAt(new int2(sector2.Bounds.MinCell.x, i))) {
                    lineSize++;
                }
                else if (!horizontal && sector1.IsOpenAt(new int2(i, sector1.Bounds.MaxCell.y)) 
                        && sector2.IsOpenAt(new int2(i, sector2.Bounds.MinCell.y))) {
                    lineSize++;
                }
                else {
                    CreateInterEdges(index1, index2, horizontal, lineSize, i);
                    lineSize = 0;
                }
            }

            //If line size > 0 after looping, then we have another line to fill in
            CreateInterEdges(index1, index2, horizontal, lineSize, i);
        }

        //i is the index at which we stopped (either its an obstacle or the end of the cluster
        private void CreateInterEdges(int index1, int index2, bool horizontal, int lineSize, int i) {
            if (lineSize > 0) {
                CreateInterEdge(index1, index2, horizontal, i - lineSize, i - 1);// i - (lineSize / 2 + 1));
            }
        }

        //Inter edges are edges that crosses GraphSectors
        private void CreateInterEdge(int index1, int index2, bool horizontal, int start, int end) {
            var sector1 = GraphSectors[index1];
            var sector2 = GraphSectors[index2];

            int mid = (start + end) / 2;
            int2 start1, start2, end1, end2, dir;
            Portal portal1, portal2;
            if (horizontal) {
                start1 = new int2(sector1.Bounds.MaxCell.x, start);
                end1 = new int2(sector1.Bounds.MaxCell.x, end);
                start2 = new int2(sector2.Bounds.MinCell.x, start);
                end2 = new int2(sector2.Bounds.MinCell.x, end);
                dir = new int2(1, 0);
            }
            else {
                start1 = new int2(start, sector1.Bounds.MaxCell.y);
                end1 = new int2(end, sector1.Bounds.MaxCell.y);
                start2 = new int2(start, sector2.Bounds.MinCell.y);
                end2 = new int2(end, sector2.Bounds.MinCell.y);
                dir = new int2(0, 1);
            }

            var mid1 = (start1 + end1) / 2;
            if (!sector1.HasExitPortalAt(mid1)) {
                portal1 = new Portal(start1, end1, sector1.Index, dir);
                sector1.AddExitPortal(portal1);
            }

            var mid2 = (start2 + end2) / 2;
            if (!sector2.HasExitPortalAt(mid2)) {
                portal2 = new Portal(start2, end2, sector2.Index, -dir);
                sector2.AddExitPortal(portal2);
            }

            var portalIndex1 = sector1.ExitPortalLookup[mid1];
            portal1 = sector1.ExitPortals[portalIndex1];
            
            var portalIndex2 = sector2.ExitPortalLookup[mid2];
            portal2 = sector2.ExitPortals[portalIndex2];

            portal1.Edges.Add(new PortalEdge() {
                start = portal1.Position,
                end = portal2.Position,
                weight = 1 
            });
            sector1.ExitPortals[portalIndex1] = portal1;

            portal2.Edges.Add(new PortalEdge() {
                start = portal2.Position,
                end = portal1.Position,
                weight = 1 
            });
            sector2.ExitPortals[portalIndex2] = portal2;

            GraphSectors[index1] = sector1;
            GraphSectors[index2] = sector2;
        }

    }

}