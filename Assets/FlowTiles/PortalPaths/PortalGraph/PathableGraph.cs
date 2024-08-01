using Unity.Burst;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    [BurstCompile]
    public struct PathableGraph {

        [BurstCompile]
        public static void BurstBuild(ref PathableGraph graph, ref PathableLevel map) {
            graph.Build(map);
        }

        // -----------------------------------------

        public SectorLayout Layout;
        public CostMap Costs;
        public PortalMap Portals;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(int2 sizeCells, int resolution) {
            Layout = new SectorLayout(sizeCells, resolution);
            Costs = new CostMap(Layout);
            Portals = new PortalMap(Layout);
        }

        public void Build(PathableLevel map) {
            Costs.Build(map);
            Portals.Build(Costs);
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

        public bool TryGetRootPortal(int cellX, int cellY, out Portal portal) {
            var color = Costs.GetColor(cellX, cellY);
            return Portals.TryGetRootPortal(cellX, cellY, color, out portal);
        }

        public bool TryGetExitPortal(int cellX, int cellY, out Portal portal) {
            return Portals.TryGetExitPortal(cellX, cellY, out portal);
        }

    }

}