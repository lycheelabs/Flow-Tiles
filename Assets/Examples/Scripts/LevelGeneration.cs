using Unity.Mathematics;

namespace FlowTiles.Examples {

    public static class LevelGeneration {

        public static void InitialiseRandomObstacles(PathableLevel level, bool clearEdges) {
            if (clearEdges) { 
                for (int x = 1; x < level.Size.x - 1; x++) {
                    for (int y = 1; y < level.Size.y - 1; y++) {
                        if (UnityEngine.Random.value < 0.2f) {
                            level.SetObstacle(x, y, true);
                        }
                    }
                }
            } else {
                for (int x = 0; x < level.Size.x; x++) {
                    for (int y = 0; y < level.Size.y; y++) {
                        if (UnityEngine.Random.value < 0.2f) {
                            level.SetObstacle(x, y, true);
                        }
                    }
                }
            }
        }

        public static void InitialiseRandomWalls(PathableLevel level, int length) {
            var sep = math.max(2, length / 4);
            // Add horizontal walls
            for (int y = 1; y < level.Size.y - 1; y += sep) {
                var minX = 1;
                var maxX = level.Size.x - 2 - length;
                var xStart = UnityEngine.Random.Range(minX, maxX);
                for (int x = 0; x < length; x++) {
                    level.SetObstacle(x + xStart, y, true);
                }
            }
            // Add vertical walls
            for (int x = 1; x < level.Size.x - 1; x += sep) {
                var minY = 1;
                var maxY = level.Size.y - 2 - length;
                var yStart = UnityEngine.Random.Range(minY, maxY);
                for (int y = 0; y < length; y++) {
                    level.SetObstacle(x, y + yStart, true);
                }
            }
        }

    }

}