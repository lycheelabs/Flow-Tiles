using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Portal {

        public readonly SectorCell Position;
        public readonly int2 LowerCorner;
        public readonly int2 UpperCorner;

        public readonly NativeList<PortalEdge> Edges;
        public readonly int2 Direction;
        public int Color;

        public Portal(int2 cell, int sector, int2 direction) {
            Position = new SectorCell(sector, cell);
            LowerCorner = cell;
            UpperCorner = cell;
            Edges = new NativeList<PortalEdge>(10, Allocator.Persistent);
            Direction = direction;
            Color = -1;
        }

        public Portal(int2 corner1, int2 corner2, int sector, int2 direction) {
            Position = new SectorCell(sector, (corner1 + corner2) / 2);
            LowerCorner = corner1;
            UpperCorner = corner2;
            Edges = new NativeList<PortalEdge>(10, Allocator.Persistent);
            Direction = direction;
            Color = -1;
        }

        public bool IsInSameCluster (Portal other) {
            return other.Position.SectorIndex == Position.SectorIndex && other.Color == Color;
        }

    }

}