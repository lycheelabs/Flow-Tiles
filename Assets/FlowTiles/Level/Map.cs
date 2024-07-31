using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {

    public struct Map {

        public readonly Boundaries Bounds;
        public readonly int NumCells;

        public NativeArray<bool> Obstacles;
        public NativeArray<byte> Costs;

        public Map(int width, int height) {
            Bounds = new Boundaries(0, new int2(width - 1, height - 1));
            NumCells = width * height;

            Obstacles = new NativeArray<bool>(NumCells, Allocator.Persistent);
            Costs = new NativeArray<byte>(NumCells, Allocator.Persistent);

            for (int i = 0; i < NumCells; i++) {
                Costs[i] = 1;
            }
        }

        public void InitialiseRandomObstacles () {
            var width = Bounds.Width;
            var height = Bounds.Height;

            for (int i = 0; i < NumCells; i++) {
                var x = i % width;
                var y = i / width;
                if (x > 0 && y > 0 && x < width - 1 && y < height - 1) {
                    if (UnityEngine.Random.value < 0.2f) {
                        Obstacles[i] = true;
                    }
                }
            }
        }

    }

}