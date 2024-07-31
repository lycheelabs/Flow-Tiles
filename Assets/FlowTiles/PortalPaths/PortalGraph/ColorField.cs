using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct ColorField {

        public readonly int2 size;
        public NativeArray<short> Colors;
        public short NumColors;

        public ColorField(int2 size) {
            this.size = size;
            Colors = new NativeArray<short>(size.x * size.y, Allocator.Persistent);
            NumColors = 0;
        }

        private int ToIndex(int x, int y) {
            return x + y * size.x;
        }

        public short GetColor(int x, int y) {
            return Colors[x + y * size.x];
        }

        public short GetColor(int2 cell) {
            return Colors[cell.x + cell.y * size.x];
        }

        // Has safety checks
        public short TryGetColor(int x, int y) {
            if (x < 0 || y < 0 || x >= size.x || y >= size.y) {
                return -1;
            }
            return Colors[x + y * size.x];
        }

        public void Recolor (CostField costs) {

            // Divide into open areas and walls
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;
                    Colors[index] = 0;

                    var cost = costs.Costs[index];
                    var blocked = cost == CostField.WALL;
                    if (blocked) Colors[index] = -1;
                }
            }

            // Fill open areas
            NumColors = 0;
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;
                    if (Colors[index] == 0) {
                        NumColors++;
                        FloodFill(new int2(x, y), 0, NumColors);
                    }
                }
            }

            // Expand fills into walls
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;
                    if (Colors[index] == -1) {
                        var c1 = TryGetColor(x - 1, y);
                        var c2 = TryGetColor(x + 1, y);
                        var c3 = TryGetColor(x, y - 1);
                        var c4 = TryGetColor(x, y + 1);
                        var bestNeighbor = math.max(math.max(c1, c2), math.max(c3, c4));
                        if (bestNeighbor > 0) {
                            FloodFill(new int2(x, y), -1, (short)bestNeighbor);
                        };
                    }
                }
            }
        }

        // Flood fill using the scanline method. Based on...
        // https://simpledevcode.wordpress.com/2015/12/29/flood-fill-algorithm-using-c-net/
        private void FloodFill(int2 startPoint, short oldColorIndex, short newColorIndex) {
            NativeStack<int2> points = new NativeStack<int2>(100, Allocator.Temp);
            points.Push(startPoint);

            while (points.Count != 0) {
                int2 temp = points.Pop();
                int y1 = temp.y;
                while (y1 >= 0 && Colors[ToIndex(temp.x, y1)] == oldColorIndex) {
                    y1--;
                }
                y1++;
                bool spanLeft = false;
                bool spanRight = false;

                while (y1 < size.y && Colors[ToIndex(temp.x, y1)] == oldColorIndex) {
                    Colors[ToIndex(temp.x, y1)] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && Colors[ToIndex(temp.x - 1, y1)] == oldColorIndex) {
                        points.Push(new int2(temp.x - 1, y1));
                        spanLeft = true;
                    }
                    else if (spanLeft && (temp.x - 1 == 0 || Colors[ToIndex(temp.x - 1, y1)] != oldColorIndex)) {
                        spanLeft = false;
                    }

                    if (!spanRight && temp.x < size.x - 1 && Colors[ToIndex(temp.x + 1, y1)] == oldColorIndex) {
                        points.Push(new int2(temp.x + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && (temp.x < size.x - 1 && Colors[ToIndex(temp.x + 1, y1)] != oldColorIndex)) {
                        spanRight = false;
                    }
                    y1++;
                }
            }
        }

    }

}