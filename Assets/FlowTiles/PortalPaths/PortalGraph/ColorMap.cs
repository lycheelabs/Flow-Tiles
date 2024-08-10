using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {
    public struct ColorMap {

        public readonly int Index;
        public readonly CellRect Bounds;

        public UnsafeField<short> Cells;
        public short NumColors;

        public ColorMap(int index, CellRect boundaries) {
            Index = index;
            Bounds = new CellRect();

            Bounds = boundaries;
            Cells = new UnsafeField<short>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 0);
            NumColors = 0;
        }

        public void Dispose() {
            Cells.Dispose();
        }

        public void CalculateColors(CostMap costs) {
            FloodFillAll(costs);
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public short GetColorAt(int2 pos) {
            var localPos = pos - Bounds.MinCell;
            return Cells[localPos.x, localPos.y];
        }

        // --------------------------------------------------------------

        private void FloodFillAll(CostMap costs) {
            var cellsInSector = Bounds.SizeCells.x * Bounds.SizeCells.y;

            // Divide into open areas and walls
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    Cells[x, y] = 0;

                    var cost = costs.Cells[x, y];
                    var blocked = cost == PathableLevel.WALL_COST;
                    if (blocked) Cells[x, y] = -1;
                }
            }

            // Fill open areas
            NumColors = 0;
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    if (Cells[x, y] == 0) {
                        NumColors++;
                        FloodFill(costs, new int2(x, y), costs.Cells[x, y], NumColors, cellsInSector);
                    }
                }
            }

            // Expand fills into walls
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    if (Cells[x, y] == -1) {
                        var c1 = TryGetColor(x - 1, y);
                        var c2 = TryGetColor(x + 1, y);
                        var c3 = TryGetColor(x, y - 1);
                        var c4 = TryGetColor(x, y + 1);
                        var bestNeighbor = math.max(math.max(c1, c2), math.max(c3, c4));
                        if (bestNeighbor > 0) {
                            FloodFill(costs, new int2(x, y), 255, (short)bestNeighbor, cellsInSector);
                        };
                    }
                }
            }
        }

        // Flood fill using the scanline method. Based on...
        // https://simpledevcode.wordpress.com/2015/12/29/flood-fill-algorithm-using-c-net/
        private void FloodFill(CostMap costs, int2 startPoint, byte targetCost, short newColorIndex, int cellsInSector) {
            NativeStack<int2> points = new NativeStack<int2>(cellsInSector, Allocator.Temp);
            NativeHashSet<int2> visited = new NativeHashSet<int2>(cellsInSector, Allocator.Temp);

            points.Push(startPoint);
            visited.Add(startPoint);

            while (points.Count != 0) {
                int2 temp = points.Pop();
                int row = temp.y;
                while (row >= 0 && costs.Cells[temp.x, row] == targetCost) {
                    row--;
                }
                row++;
                bool spanLeft = false;
                bool spanRight = false;

                while (row < Cells.Size.y && costs.Cells[temp.x, row] == targetCost) {
                    Cells[temp.x, row] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && costs.Cells[temp.x - 1, row] == targetCost) {
                        var next = new int2(temp.x - 1, row);
                        if (!visited.Contains(next)) {
                            visited.Add(next);
                            points.Push(next);
                        }
                        spanLeft = true;
                    }
                    else if (spanLeft && (temp.x - 1 == 0 || costs.Cells[temp.x - 1, row] != targetCost)) {
                        spanLeft = false;
                    }

                    if (!spanRight && temp.x < Cells.Size.x - 1 && costs.Cells[temp.x + 1, row] == targetCost) {
                        var next = new int2(temp.x + 1, row);
                        if (!visited.Contains(next)) {
                            visited.Add(next);
                            points.Push(next);
                        }
                        spanRight = true;
                    }
                    else if (spanRight && (temp.x < Cells.Size.x - 1 && costs.Cells[temp.x + 1, row] != targetCost)) {
                        spanRight = false;
                    }
                    row++;
                }
            }
        }

        private short TryGetColor(int x, int y) {
            if (x < 0 || y < 0 || x >= Cells.Size.x || y >= Cells.Size.y) {
                return -1;
            }
            return Cells[x, y];
        }

        public int FindIslandOfColor(int color, IslandMap islands) {
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    if (Cells[x, y] == color) return islands.Cells[x, y];
                }
            }
            return -1;
        }
    }

}