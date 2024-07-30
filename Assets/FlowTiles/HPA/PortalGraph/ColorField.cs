using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct ColorField {

        public int2 size;
        public NativeArray<short> Colors;
        public short NumColors;

        public ColorField(int2 size) {
            this.size = size;
            Colors = new NativeArray<short>(size.x * size.y, Allocator.Persistent);
            NumColors = 0;
        }

        public short GetColor(int x, int y) {
            return Colors[x + y * size.x];
        }

        public short GetColor(int2 cell) {
            return Colors[cell.x + cell.y * size.x];
        }

        public void Recolor (CostField costs) {
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;
                    Colors[index] = 0;

                    var cost = costs.Costs[index];
                    var blocked = cost == CostField.WALL;
                    if (blocked) Colors[index] = -1;
                }
            }

            NumColors = 0;
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;
                    if (Colors[index] == 0) {
                        NumColors++;
                        FloodFill(new int2(x, y), NumColors);
                    }
                }
            }

        }

        private void FloodFill(int2 startPoint, short newColorIndex) {
            Stack<int2> points = new Stack<int2>();
            points.Push(startPoint);

            while (points.Count != 0) {
                int2 temp = points.Pop();
                int y1 = temp.y;
                while (y1 >= 0 && Colors[ToIndex(temp.x, y1)] == 0) {
                    y1--;
                }
                y1++;
                bool spanLeft = false;
                bool spanRight = false;

                while (y1 < size.y && Colors[ToIndex(temp.x, y1)] == 0) {
                    Colors[ToIndex(temp.x, y1)] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && Colors[ToIndex(temp.x - 1, y1)] == 0) {
                        points.Push(new int2(temp.x - 1, y1));
                        spanLeft = true;
                    }
                    else if (spanLeft && temp.x - 1 == 0 && Colors[ToIndex(temp.x - 1, y1)] != 0) {
                        spanLeft = false;
                    }
                    if (!spanRight && temp.x < size.x - 1 && Colors[ToIndex(temp.x + 1, y1)] == 0) {
                        points.Push(new int2(temp.x + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && temp.x < size.x - 1 && Colors[ToIndex(temp.x + 1, y1)] != 0) {
                        spanRight = false;
                    }
                    y1++;
                }
            }
        }

        private int ToIndex (int x, int y) {
            return x + y * size.x;
        }

    }

}