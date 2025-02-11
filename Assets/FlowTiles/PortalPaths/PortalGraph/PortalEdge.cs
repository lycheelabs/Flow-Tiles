using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalEdge {

        public SectorCell start;
        public SectorCell end;
        public float weight;
        public bool isExit;

        public bool SpansTwoSectors => end.SectorIndex != start.SectorIndex;
        public int2 Span => end.Cell - start.Cell;

    }

}