using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {

    public struct PathableMap {

        public readonly int2 Size;
        public readonly CellRect Bounds;
        public UnsafeField<bool> Obstacles;
        public UnsafeField<byte> BaseCosts;
        public UnsafeField<byte> ModifiedCosts;

        public PathableMap(int width, int height) {
            Size = new int2(width, height);
            Bounds = new CellRect(0, Size - 1);
            Obstacles = new UnsafeField<bool>(Size, Allocator.Persistent);
            BaseCosts = new UnsafeField<byte>(Size, Allocator.Persistent, initialiseTo: 1);
            ModifiedCosts = new UnsafeField<byte>(Size, Allocator.Persistent, initialiseTo: 0);
        }

        public void InitialiseRandomObstacles () {
            for (int x = 1; x < Size.x - 1; x++) {
                for (int y = 1; y < Size.y - 1; y++) {
                    if (UnityEngine.Random.value < 0.2f) {
                        Obstacles[x, y] = true;
                    }
                }
            }
        }

        public byte GetCostAt (int x, int y) {
            var obstacle = Obstacles[x, y];
            if (obstacle) {
                return CostField.WALL;
            }

            var modifiedCost = ModifiedCosts[x, y];
            if (modifiedCost > 0) {
                return modifiedCost;
            }

            return BaseCosts[x, y];
        }

    }

}