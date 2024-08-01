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
        public CostSectors Costs;
        public GraphSectors Graph;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(int2 sizeCells, int resolution) {
            Layout = new SectorLayout(sizeCells, resolution);
            Costs = new CostSectors(Layout);
            Graph = new GraphSectors(Layout);
        }

        public void Build(PathableLevel map) {
            Costs.Build(map);
            Graph.Build(Costs);
        }

        public CostSector GetMapSector(int cellX, int cellY) {
            return Costs.GetSector(cellX, cellY);
        }

        public int GetColor(int cellX, int cellY) {
            return Costs.GetColor(cellX, cellY);
        }

        public GraphSector GetGraphSector(int cellX, int cellY) {
            return Graph.GetSector(cellX, cellY);
        }

        public bool TryGetClusterRoot(int cellX, int cellY, out Portal cluster) {
            var color = Costs.GetColor(cellX, cellY);
            return Graph.TryGetClusterRoot(cellX, cellY, color, out cluster);
        }

    }

}