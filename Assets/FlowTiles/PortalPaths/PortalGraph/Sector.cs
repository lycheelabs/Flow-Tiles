using Unity.Collections;
using FlowTiles.Utils;

namespace FlowTiles.PortalPaths {
    public struct Sector {

        public readonly int Index;
        public readonly int Version;
        public readonly CellRect Bounds;
        public UnsafeArray<SectorMap> Maps;

        public Sector(int index, int version, CellRect boundaries, PathableLevel level, int numTravelTypes) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            Maps = new UnsafeArray<SectorMap>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < Maps.Length; i++) {
                var map = new SectorMap(Index, Bounds, i, version);
                map.Costs.Initialise(level);
                Maps[i] = map;
            }
        }

        public bool IsCreated => Maps.IsCreated;

        public void Dispose () {
            for (int i = 0; i < Maps.Length; i++) {
                Maps[i].Dispose();
            }
            Maps.Dispose();
        }

    }

}