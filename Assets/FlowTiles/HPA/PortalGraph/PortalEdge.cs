using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct PortalEdge {

        public SectorCell start;
        public SectorCell end;
        public float weight;

        public bool SpansTwoSectors => end.SectorIndex != start.SectorIndex;
        public int2 Span => end.Cell - start.Cell;

    }

}