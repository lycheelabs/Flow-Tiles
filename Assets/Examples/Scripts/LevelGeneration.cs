using Unity.Mathematics;

namespace FlowTiles.Examples {

    public static class LevelGeneration {

        public static void InitialiseRandomObstacles(PathableLevel level, bool clearEdges) {
            if (clearEdges) { 
                for (int x = 1; x < level.Size.x - 1; x++) {
                    for (int y = 1; y < level.Size.y - 1; y++) {
                        if (UnityEngine.Random.value < 0.2f) {
                            level.SetBlocked(x, y, true);
                        }
                    }
                }
            } else {
                for (int x = 0; x < level.Size.x; x++) {
                    for (int y = 0; y < level.Size.y; y++) {
                        if (UnityEngine.Random.value < 0.2f) {
                            level.SetBlocked(x, y, true);
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
                    level.SetBlocked(x + xStart, y, true);
                }
            }
            // Add vertical walls
            for (int x = 1; x < level.Size.x - 1; x += sep) {
                var minY = 1;
                var maxY = level.Size.y - 2 - length;
                var yStart = UnityEngine.Random.Range(minY, maxY);
                for (int y = 0; y < length; y++) {
                    level.SetBlocked(x, y + yStart, true);
                }
            }
        }

        public static void InitialiseWaterPools(PathableLevel level) {
            var size = level.Size;
            var sizeSectors = level.Layout.SizeSectors;
            var resolution = level.Layout.Resolution;

            for (int sector = 0; sector < sizeSectors.x; sector++) {
                var x0 = sector * resolution;
                var x1 = x0 + resolution;
                for (int x = x0 + 2; x < x1 - 2; x++) {
                    var yGap = sector * resolution + resolution / 2;
                    for (int y = 2; y < size.y - 2; y++) {
                        var gapDist = math.abs(yGap - y);
                        if (gapDist >= 2) {
                            level.SetTerrain(x, y, (byte)TerrainType.WATER);
                        }
                    }
                }
            }
            
        }

    }

}