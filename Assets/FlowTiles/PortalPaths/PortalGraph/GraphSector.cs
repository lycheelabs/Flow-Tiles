using Unity.Collections;
using FlowTiles.Utils;

namespace FlowTiles.PortalPaths {

    /// <summary>
    /// Contains all the data representing a sub-region of the PathableGraph.
    /// A different copy of the data is kept for each travel type.
    /// </summary>
    public struct GraphSector {

        public readonly int Index;
        public readonly int Version;
        public readonly CellRect Bounds;
        public UnsafeArray<SectorData> DataSets;

        public GraphSector(int index, int version, CellRect boundaries, PathableLevel level, int numTravelTypes) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            DataSets = new UnsafeArray<SectorData>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < DataSets.Length; i++) {
                var data = new SectorData(Index, Bounds, i, version);
                data.Costs.Initialise(level);
                DataSets[i] = data;
            }
        }

        public bool IsCreated => DataSets.IsCreated;

        public SectorData GetData (int travelType) {
            return DataSets[travelType];
        }

        public void UpdateData(int travelType, SectorData data) {
            DataSets[travelType] = data;
        }

        public void Dispose () {
            for (int i = 0; i < DataSets.Length; i++) {
                DataSets[i].Dispose();
            }
            DataSets.Dispose();
        }

    }

}