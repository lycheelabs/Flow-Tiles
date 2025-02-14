using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowTiles.PortalPaths {
    public struct SectorRoot {

        public readonly int SectorIndex;
        public readonly int Island;
        public int Continent;

        public UnsafeList<SectorCell> Portals;

        public SectorRoot(int sector, int island) {
            SectorIndex = sector;
            Island = island;
            Continent = -1;
            Portals = new UnsafeList<SectorCell>(Constants.EXPECTED_MAX_EDGES, Allocator.Persistent);
        }

        public void Dispose() {
            Portals.Dispose();
        }

        public bool ConnectsToPortal(Portal portal) {
            return portal.Center.SectorIndex == SectorIndex && portal.Island == Island;
        }

        public bool Matches(SectorRoot other) {
            return SectorIndex == other.SectorIndex && Island == other.Island;
        }

    }

}