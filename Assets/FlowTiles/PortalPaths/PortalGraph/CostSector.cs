using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct CostSector {

        public readonly int Index;
        public readonly CellRect Bounds;

        public UnsafeField<byte> Costs;
        public UnsafeField<short> Colors;
        public short NumColors;

        public CostSector(int index, CellRect boundaries) {
            Index = index;
            Bounds = new CellRect();

            Bounds = boundaries;
            Costs = new UnsafeField<byte>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 1);
            Colors = new UnsafeField<short>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 0);
            NumColors = 0;
        }

        public void Initialise(PathableLevel map) {
            CopyCosts(map, Bounds.MinCell);
        }

        public void Process () {
            CalculateColors();
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool IsOpenAt(int2 pos) {
            var localPos = pos - Bounds.MinCell;
            return Contains(pos)
                && Costs[localPos.x, localPos.y] != PathableLevel.WALL_COST;
        }

        // --------------------------------------------------------------

        private void CopyCosts(PathableLevel map, int2 corner) {
            for (int x = 0; x < Costs.Size.x; x++) {
                for (var y = 0; y < Costs.Size.y; y++) {
                    Costs[x, y] = map.GetCostAt(corner.x + x, corner.y + y);
                }
            }
        }

        private void CalculateColors() {
            var cellsInSector = Bounds.SizeCells.x * Bounds.SizeCells.y;

            // Divide into open areas and walls
            for (int x = 0; x < Colors.Size.x; x++) {
                for (var y = 0; y < Colors.Size.y; y++) {
                    Colors[x, y] = 0;

                    var cost = Costs[x, y];
                    var blocked = cost == PathableLevel.WALL_COST;
                    if (blocked) Colors[x, y] = -1;
                }
            }

            // Fill open areas
            NumColors = 0;
            for (int x = 0; x < Colors.Size.x; x++) {
                for (var y = 0; y < Colors.Size.y; y++) {
                    if (Colors[x, y] == 0) {
                        NumColors++;
                        FloodFill(new int2(x, y), 0, NumColors, cellsInSector);
                    }
                }
            }

            // Expand fills into walls
            for (int x = 0; x < Colors.Size.x; x++) {
                for (var y = 0; y < Colors.Size.y; y++) {
                    if (Colors[x, y] == -1) {
                        var c1 = TryGetColor(x - 1, y);
                        var c2 = TryGetColor(x + 1, y);
                        var c3 = TryGetColor(x, y - 1);
                        var c4 = TryGetColor(x, y + 1);
                        var bestNeighbor = math.max(math.max(c1, c2), math.max(c3, c4));
                        if (bestNeighbor > 0) {
                            FloodFill(new int2(x, y), -1, (short)bestNeighbor, cellsInSector);
                        };
                    }
                }
            }
        }

        // Flood fill using the scanline method. Based on...
        // https://simpledevcode.wordpress.com/2015/12/29/flood-fill-algorithm-using-c-net/
        private void FloodFill(int2 startPoint, short oldColorIndex, short newColorIndex, int cellsInSector) {
            NativeStack<int2> points = new NativeStack<int2>(cellsInSector, Allocator.Temp);
            points.Push(startPoint);

            while (points.Count != 0) {
                int2 temp = points.Pop();
                int y1 = temp.y;
                while (y1 >= 0 && Colors[temp.x, y1] == oldColorIndex) {
                    y1--;
                }
                y1++;
                bool spanLeft = false;
                bool spanRight = false;

                while (y1 < Colors.Size.y && Colors[temp.x, y1] == oldColorIndex) {
                    Colors[temp.x, y1] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && Colors[temp.x - 1, y1] == oldColorIndex) {
                        points.Push(new int2(temp.x - 1, y1));
                        spanLeft = true;
                    }
                    else if (spanLeft && (temp.x - 1 == 0 || Colors[temp.x - 1, y1] != oldColorIndex)) {
                        spanLeft = false;
                    }

                    if (!spanRight && temp.x < Colors.Size.x - 1 && Colors[temp.x + 1, y1] == oldColorIndex) {
                        points.Push(new int2(temp.x + 1, y1));
                        spanRight = true;
                    }
                    else if (spanRight && (temp.x < Colors.Size.x - 1 && Colors[temp.x + 1, y1] != oldColorIndex)) {
                        spanRight = false;
                    }
                    y1++;
                }
            }
        }

        private short TryGetColor(int x, int y) {
            if (x < 0 || y < 0 || x >= Colors.Size.x || y >= Colors.Size.y) {
                return -1;
            }
            return Colors[x, y];
        }

    }

}