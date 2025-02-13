using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct Portal {

        public readonly SectorCell Center;
        public readonly CellRect Bounds;

        public int Island;
        public UnsafeList<PortalEdge> Edges;

        public Portal(int2 corner1, int2 corner2, int sector) {
            Center = new SectorCell(sector, (corner1 + corner2) / 2);
            Bounds = new CellRect(corner1, corner2);
            Edges = new UnsafeList<PortalEdge>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
            Island = -1;
        }

        public void Dispose() {
            Edges.Dispose();
        }

    }

    public struct SectorRoot {

        public readonly int SectorIndex;
        public readonly int IslandIndex;
        public UnsafeList<SectorCell> Portals;

        public SectorRoot(int sector, int island) {
            SectorIndex = sector;
            IslandIndex = island;
            Portals = new UnsafeList<SectorCell>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
        }

        public void Dispose() {
            Portals.Dispose();
        }

        public bool ConnectsToPortal(Portal portal) {
            return portal.Center.SectorIndex == SectorIndex && portal.Island == IslandIndex;
        }

    }

}