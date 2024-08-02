using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Portal {

        public readonly SectorCell Position;
        public readonly CellRect Bounds;

        public UnsafeList<PortalEdge> Edges;
        public int Color;

        public Portal(int2 cell, int sector, int2 direction) {
            Position = new SectorCell(sector, cell);
            Bounds = new CellRect(cell, cell);
            Edges = new UnsafeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
        }

        public Portal(int2 corner1, int2 corner2, int sector, int2 direction) {
            Position = new SectorCell(sector, (corner1 + corner2) / 2);
            Bounds = new CellRect(corner1, corner2);
            Edges = new UnsafeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
        }

        public bool IsSamePortal (Portal other) {
            return other.Position.Equals(Position) && other.Color == Color;
        }

        public bool IsInSameCluster (Portal other) {
            return other.Position.SectorIndex == Position.SectorIndex && other.Color == Color;
        }

    }

}