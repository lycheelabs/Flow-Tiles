using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct PortalEdge {

        public int startSector;
        public int2 startCell;

        public int endSector;
        public int2 endCell;

        public float weight;

    }

}