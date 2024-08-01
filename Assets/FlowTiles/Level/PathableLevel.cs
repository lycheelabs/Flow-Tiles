using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {

    public struct PathableLevel {

        public const byte WALL_COST = 255;

        public readonly int2 Size;
        public readonly CellRect Bounds;
        public NativeField<bool> Obstacles;
        public NativeField<byte> BaseCosts;
        public NativeField<byte> ModifiedCosts;

        public PathableLevel(int width, int height) {
            Size = new int2(width, height);
            Bounds = new CellRect(0, Size - 1);
            Obstacles = new NativeField<bool>(Size, Allocator.Persistent);
            BaseCosts = new NativeField<byte>(Size, Allocator.Persistent, initialiseTo: 1);
            ModifiedCosts = new NativeField<byte>(Size, Allocator.Persistent);
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
                return WALL_COST;
            }

            var modifiedCost = ModifiedCosts[x, y];
            if (modifiedCost > 0) {
                return modifiedCost;
            }

            return BaseCosts[x, y];
        }

    }

}