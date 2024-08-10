using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct CostMap {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int MovementType;

        public UnsafeField<byte> Costs;

        public CostMap(int index, CellRect boundaries, int movementType) {
            Index = index;
            Bounds = new CellRect();
            MovementType = movementType;

            Bounds = boundaries;
            Costs = new UnsafeField<byte>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 1);
        }

        public void Initialise(PathableLevel map) {
            CopyCosts(map, Bounds.MinCell);
        }

        public void Dispose() {
            Costs.Dispose();
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
                && Costs[localPos.x, localPos.y] < PathableLevel.WALL_COST;
        }

        public byte GetCostAt(int2 pos) {
            var localPos = pos - Bounds.MinCell;
            return Costs[localPos.x, localPos.y];
        }

        // --------------------------------------------------------------

        private void CopyCosts(PathableLevel map, int2 corner) {
            for (int x = 0; x < Costs.Size.x; x++) {
                for (var y = 0; y < Costs.Size.y; y++) {
                    Costs[x, y] = map.GetCostAt(corner.x + x, corner.y + y, MovementType);
                }
            }
        }

    }

    public struct ColorMap {

        public readonly int Index;
        public readonly CellRect Bounds;

        public UnsafeField<short> Colors;
        public short NumColors;

        public ColorMap(int index, CellRect boundaries) {
            Index = index;
            Bounds = new CellRect();

            Bounds = boundaries;
            Colors = new UnsafeField<short>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 0);
            NumColors = 0;
        }

        public void Dispose() {
            Colors.Dispose();
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
            return Colors[localPos.x, localPos.y];
        }

        // --------------------------------------------------------------

        private void FloodFillAll(CostMap costs) {
            var cellsInSector = Bounds.SizeCells.x * Bounds.SizeCells.y;

            // Divide into open areas and walls
            for (int x = 0; x < Colors.Size.x; x++) {
                for (var y = 0; y < Colors.Size.y; y++) {
                    Colors[x, y] = 0;

                    var cost = costs.Costs[x, y];
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
                        FloodFill(costs, new int2(x, y), costs.Costs[x, y], NumColors, cellsInSector);
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
                while (row >= 0 && costs.Costs[temp.x, row] == targetCost) {
                    row--;
                }
                row++;
                bool spanLeft = false;
                bool spanRight = false;

                while (row < Colors.Size.y && costs.Costs[temp.x, row] == targetCost) {
                    Colors[temp.x, row] = newColorIndex;

                    if (!spanLeft && temp.x > 0 && costs.Costs[temp.x - 1, row] == targetCost) {
                        var next = new int2(temp.x - 1, row);
                        if (!visited.Contains(next)) {
                            visited.Add(next);
                            points.Push(next);
                        }
                        spanLeft = true;
                    }
                    else if (spanLeft && (temp.x - 1 == 0 || costs.Costs[temp.x - 1, row] != targetCost)) {
                        spanLeft = false;
                    }

                    if (!spanRight && temp.x < Colors.Size.x - 1 && costs.Costs[temp.x + 1, row] == targetCost) {
                        var next = new int2(temp.x + 1, row);
                        if (!visited.Contains(next)) {
                            visited.Add(next);
                            points.Push(next);
                        }
                        spanRight = true;
                    }
                    else if (spanRight && (temp.x < Colors.Size.x - 1 && costs.Costs[temp.x + 1, row] != targetCost)) {
                        spanRight = false;
                    }
                    row++;
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