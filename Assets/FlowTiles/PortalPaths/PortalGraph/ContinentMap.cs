using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.PortalPaths {

    public struct ContinentMap {

        private PathableGraph Graph;

        public ContinentMap (PathableGraph graph) {
            Graph = graph;
        }

        // No disposal method because the creator of the PathableGraph is responsible for disposing it.

        public bool TryGetContinentAt(float2 position, int travelType, out int continent) {
            return Graph.TryExtractContinent(position, travelType, out continent);
        }

    }

}