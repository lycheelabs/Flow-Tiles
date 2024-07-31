﻿using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct CostField {

        public const byte WALL = 255;
        public const byte OPEN = 1;

        public readonly int2 size;
        public NativeArray<byte> Costs;

        public CostField(int2 size) {
            this.size = size;
            Costs = new NativeArray<byte> (size.x * size.y, Allocator.Persistent);
        }

        public byte GetCost(int x, int y) {
            return Costs[x + y * size.x];
        }

        public byte GetCost(int2 cell) {
            return Costs[cell.x + cell.y * size.x];
        }

        public void Initialise(Map map, int2 corner) {
            var mapWidth = map.Bounds.Width;
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var mapIndex = (corner.x + x) + (corner.y + y) * mapWidth;
                    var sectorIndex = x + y * size.x;
                    Costs[sectorIndex] = map.Costs[mapIndex];

                    if (map.Obstacles[mapIndex]) {
                        Costs[sectorIndex] = WALL;
                    }
                }
            }
        }

    }

}