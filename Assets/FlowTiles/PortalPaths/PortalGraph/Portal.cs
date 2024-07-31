using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Portal {

        public readonly SectorCell Position;
        public readonly Boundaries Bounds;

        public readonly NativeList<PortalEdge> Edges;
        public int Color;

        public Portal(int2 cell, int sector, int2 direction) {
            Position = new SectorCell(sector, cell);
            Bounds = new Boundaries(cell, cell);
            Edges = new NativeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
        }

        public Portal(int2 corner1, int2 corner2, int sector, int2 direction) {
            Position = new SectorCell(sector, (corner1 + corner2) / 2);
            Bounds = new Boundaries(corner1, corner2);
            Edges = new NativeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
        }

        public bool IsInSameCluster (Portal other) {
            return other.Position.SectorIndex == Position.SectorIndex && other.Color == Color;
        }

    }

}