using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct Portal {

        public readonly SectorCell Position;
        public readonly CellRect Bounds;

        public int Color;
        public int Island;
        public UnsafeList<PortalEdge> Edges;

        public Portal(int2 cell, int sector) {
            Position = new SectorCell(sector, cell);
            Bounds = new CellRect(cell, cell);
            Edges = new UnsafeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
            Island = -1;
        }

        public Portal(int2 corner1, int2 corner2, int sector) {
            Position = new SectorCell(sector, (corner1 + corner2) / 2);
            Bounds = new CellRect(corner1, corner2);
            Edges = new UnsafeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Color = -1;
            Island = -1;
        }

        public void Dispose() {
            Edges.Dispose();
        }

        public bool IsSamePortal (Portal other) {
            return other.Position.Equals(Position);
        }

        public bool Matches(Portal destCluster) {
            return IsInSameIsland(destCluster) && Position.Cell.Equals(destCluster.Position.Cell);
        }

        public bool IsInSameIsland(Portal other) {
            return other.Position.SectorIndex == Position.SectorIndex && other.Island == Island;
        }

    }

}