using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    [BurstCompile]
    public struct PathableGraph {

        [BurstCompile]
        public static void BurstBuild(ref PathableGraph graph, ref PathableLevel map) {
            graph.Initialise(map);

            // To parallelise
            var pathfinder = new SectorPathfinder(graph.Layout.NumCellsInSector, Allocator.Temp);
            for (int index = 0; index < graph.Layout.NumSectorsInLevel; ++index) {
                graph.BuildSector(index, pathfinder);
            }
        }

        // -----------------------------------------

        public CellRect Bounds;
        public SectorLayout Layout;
        public CostMap Costs;
        public PortalMap Portals;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(int2 sizeCells, int resolution) {
            Bounds = new CellRect(0, sizeCells - 1);
            Layout = new SectorLayout(sizeCells, resolution);
            Costs = new CostMap(Layout);
            Portals = new PortalMap(Layout);
        }

        public void Initialise(PathableLevel map) {
            Costs.Initialise(map);
            Portals.Initialise(Costs);
        }

        public void BuildSector (int index, SectorPathfinder pathfinder) {
            var costSector = Costs.Sectors[index];
            var portalSector = Portals.Sectors[index];
            costSector.CalculateColors();
            portalSector.BuildInternalConnections(costSector, pathfinder);
            Costs.Sectors[index] = costSector;
            Portals.Sectors[index] = portalSector;
        }

        public CostSector GetCostSector(int cellX, int cellY) {
            return Costs.GetSector(cellX, cellY);
        }

        public int GetCellColor(int cellX, int cellY) {
            return Costs.GetColor(cellX, cellY);
        }

        public PortalSector GetPortalSector(int cellX, int cellY) {
            return Portals.GetSector(cellX, cellY);
        }

        public Portal GetRootPortal(int cellX, int cellY) {
            var color = Costs.GetColor(cellX, cellY);
            return Portals.GetRootPortal(cellX, cellY, color);
        }

        public bool TryGetExitPortal(int cellX, int cellY, out Portal portal) {
            return Portals.TryGetExitPortal(cellX, cellY, out portal);
        }

    }

}