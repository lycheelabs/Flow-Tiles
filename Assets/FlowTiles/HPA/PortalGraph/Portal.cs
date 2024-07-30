using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Portal {

        public readonly SectorCell Position;
        public readonly NativeList<PortalEdge> Edges;
        public readonly int2 Direction;
        public int Color;

        public Portal(int2 cell, int sector, int2 direction) {
            Position = new SectorCell(sector, cell);
            Edges = new NativeList<PortalEdge>(10, Allocator.Persistent);
            Direction = direction;
            Color = -1;
        }

        public bool IsInSameCluster (Portal other) {
            return other.Position.SectorIndex == Position.SectorIndex && other.Color == Color;
        }

    }

}