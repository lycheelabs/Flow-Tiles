using Unity.Collections;
using LycheeLabs.FlowTiles.Utils;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace LycheeLabs.FlowTiles.PortalPaths {

    /// <summary>
    /// Contains all the data representing a sub-region of the PathableGraph.
    /// A different copy of the data is kept for each travel type.
    /// </summary>
    public struct GraphSector {

        public readonly int Index;
        public readonly int Version;
        public readonly CellRect Bounds;
        public UnsafeArray<SectorData> DataSets;
        public bool IsCreated;

        public SectorData GetData (int travelType) => DataSets[travelType];

        public GraphSector(int index, int version, CellRect boundaries, ref PathableGrid levelGrid, int numTravelTypes) {
            IsCreated = true;
            Index = index;
            Bounds = boundaries;
            Version = version;

            DataSets = new UnsafeArray<SectorData>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < DataSets.Length; i++) {
                var data = new SectorData(Index, Bounds, i, version);
                data.Initialise(ref levelGrid);
                DataSets[i] = data;
            }
        }

        public void Calculate() {
            var size = Bounds.WidthCells * Bounds.HeightCells;
            SectorPathfinder pathfinder = new SectorPathfinder(size, Allocator.Temp);
            for (int travelType = 0; travelType < DataSets.Length; travelType++) {
                var data = DataSets[travelType];
                data.Islands.CalculateIslands(data.Costs);
                data.Portals.BuildInternalConnections(data, ref pathfinder);
                DataSets[travelType] = data;
            }
            pathfinder.Dispose();
        }

        public void Dispose () {
            if (!IsCreated) return;
            IsCreated = false;

            for (int i = 0; i < DataSets.Length; i++) {
                DataSets[i].Dispose();
            }
            if (DataSets.IsCreated) DataSets.Dispose();
        }

    }

}