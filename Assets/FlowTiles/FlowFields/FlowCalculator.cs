using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using System.Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.FlowField {

    [BurstCompile]
    public struct FlowCalculator {

        private static float Inf => 1000000000;
        private static float ObstacleStep => 10000;
        private static float ObstacleOverrideValue => 1;

        private const int NeighborsCount = 4;

        [BurstCompile]
        public static void BurstCalculate (ref FlowCalculator calculator) {
            calculator.Calculate();
        }

        // ------------------------------------------------------------

        [ReadOnly] public int2 Size;
        [ReadOnly] public CostSector Sector;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public float2 ExitDirection;

        // Result
        public UnsafeField<float2> Flow;
        public short Color;

        public FlowCalculator(CostSector sector, CellRect goalBounds, int2 exitDirection) {
            Size = sector.Bounds.SizeCells;
            Sector = sector;
            GoalBounds = goalBounds;
            ExitDirection = exitDirection;
            Flow = new UnsafeField<float2>(Size + 2, Allocator.Persistent);
            Color = 0;
        }

        public void Calculate() {

            var sectorBounds = Sector.Bounds;
            var size = sectorBounds.SizeCells;
            var w = size.x;
            var h = size.y;

            var goalMin = GoalBounds.MinCell - sectorBounds.MinCell;
            var goalMax = GoalBounds.MaxCell - sectorBounds.MinCell;
            Color = Sector.Colors[goalMin.x, goalMin.y];

            var speeds = new NativeField<float>(size + 2, Allocator.Temp);
            var targets = new NativeField<float2>(Size + 2, Allocator.Temp);
            var distances = new NativeField<float>(Size + 2, Allocator.Temp, initialiseTo: Inf);
            var queue = new NativeQueue<int2>(Allocator.Temp);
            var secondQueue = new NativeQueue<int2>(Allocator.Temp);
            var thirdQueue = new NativeQueue<int2>(Allocator.Temp);
            var processedPositions = new NativeArray<bool>((Size.x + 2) * (Size.y + 2), Allocator.Temp);

            // Initialise flow speeds
            for (var x = 1; x <= w; x++) {
                for (var y = 1; y <= h; y++) {
                    var cost = Sector.Costs[x - 1, y - 1];
                    var color = Sector.Colors[x - 1, y - 1];
                    speeds[x, y] = 1f / cost;
                    if (cost == PathableLevel.WALL_COST || color != Color) {
                        speeds[x, y] = -1;
                    }
                }
            }

            // Initialise the goal wavefront
            var sourcesCount = 0;
            for (int x = goalMin.x; x <= goalMax.x; x++) {
                for (int y = goalMin.y; y <= goalMax.y; y++) {
                    var source = new int2(x + 1, y + 1);
                    var index = GetIndex(source.x, source.y);

                    distances[source.x, source.y] = 0;
                    targets[source.x, source.y] = new float2(source.x, source.y);
                    Flow[source.x, source.y] = ExitDirection;
                    processedPositions[index] = true;

                    //queue.Enqueue(source);
                    var sourceSpeed = speeds[source.x, source.y];
                    for (var i = 0; i < NeighborsCount; i++) {
                        if (TryGetNeighbor(source, i, out var neighbor)) {
                            var neighborWorldCell = neighbor - 1 + Sector.Bounds.MinCell;
                            if (GoalBounds.ContainsCell(neighborWorldCell)) {
                                continue;
                            }

                            if (sourceSpeed == 0) sourcesCount++;
                            var neighborSpeed = speeds[neighbor.x, neighbor.y];
                            var q = neighborSpeed > 0 ? queue : secondQueue;
                            q.Enqueue(neighbor);
                            processedPositions[GetIndex(neighbor)] = true;
                        }
                    }
                }
            }

            // Iterate the wavefront expansion
            const int iterationLimit = 500000;
            var iterationsCount = 0;

            while ((queue.Count > 0 || secondQueue.Count > 0) && iterationsCount < iterationLimit) {
                while (queue.Count > 0 && iterationsCount++ < iterationLimit) {
                    var current = queue.Dequeue();
                    var update = UpdatePoint(current.x, current.y, ref speeds, ref targets, ref distances);
                    distances[current.x, current.y] = update;

                    if (sourcesCount > 0) {
                        sourcesCount--;
                        Flow[current.x, current.y] = float2.zero;
                        targets[current.x, current.y] = new float2(current.x, current.y);
                    }

                    for (var i = 0; i < NeighborsCount; i++) {
                        if (TryGetNeighbor(current, i, out var neighbor) &&
                            processedPositions[GetIndex(neighbor)] == false) {
                            var q = speeds[neighbor.x, neighbor.y] > 0f ? queue : secondQueue;
                            processedPositions[GetIndex(neighbor)] = true;
                            q.Enqueue(neighbor);
                        }
                    }
                }

                while (secondQueue.Count > 0 && iterationsCount++ < iterationLimit) {
                    var current = secondQueue.Dequeue();
                    var update = UpdatePoint(current.x, current.y, ref speeds, ref targets, ref distances);
                    distances[current.x, current.y] = update + ObstacleStep;
                    for (var i = 0; i < NeighborsCount; i++) {
                        if (TryGetNeighbor(current, i, out var neighbor) &&
                            processedPositions[GetIndex(neighbor)] == false) {
                            var q = speeds[neighbor.x, neighbor.y] > 0f ? queue : thirdQueue;
                            processedPositions[GetIndex(neighbor)] = true;
                            q.Enqueue(neighbor);
                        }
                    }
                }

                while (thirdQueue.Count > 0) secondQueue.Enqueue(thirdQueue.Dequeue());

                sourcesCount = queue.Count;
            }

        }

        private float UpdatePoint(int x, int y, ref NativeField<float> Speeds, ref NativeField<float2> Targets, ref NativeField<float> Distances) {
            var f = Speeds[x, y];
            if (f == 0) f = ObstacleOverrideValue;

            var minX = math.min(Distances[x - 1, y], Distances[x + 1, y]);
            var minY = math.min(Distances[x, y - 1], Distances[x, y + 1]);

            if (math.abs(minX - minY) >= 1 / f) {
                var result = math.min(minX + 1 / f, minY + 1 / f);
                if (minX < minY) {
                    Flow[x, y] = Distances[x - 1, y] < Distances[x + 1, y]
                        ? new float2(-1, 0)
                        : new float2(1, 0);
                }
                else {
                    Flow[x, y] = Distances[x, y - 1] < Distances[x, y + 1]
                        ? new float2(0, -1)
                        : new float2(0, 1);
                }

                var dir = Flow[x, y];
                var prev = new int2(x, y) + new int2((int)dir.x, (int)dir.y);

                if (Flow[x, y].Equals(Flow[prev.x, prev.y])) {
                    Targets[x, y] = Targets[prev.x, prev.y];
                }
                else {
                    Targets[x, y] = new float2(x, y) + dir;
                }

                return result;
            }
            else {
                var usedX = Distances[x - 1, y] < Distances[x + 1, y] ? x - 1 : x + 1;
                var usedY = Distances[x, y - 1] < Distances[x, y + 1] ? y - 1 : y + 1;

                var directionX = Flow[usedX, y];
                var directionY = Flow[x, usedY];

                if (TryIntersectRays(new float2(usedX, y), directionX, new float2(x, usedY),
                        directionY, out var intersectionPoint) == false) {
                    var r1 = math.min(minX + 1 / f, minY + 1 / f);
                    if (minX < minY) {
                        Flow[x, y] = Distances[x - 1, y] < Distances[x + 1, y]
                            ? new float2(-1, 0)
                            : new float2(1, 0);
                    }
                    else {
                        Flow[x, y] = Distances[x, y - 1] < Distances[x, y + 1]
                            ? new float2(0, -1)
                            : new float2(0, 1);
                    }

                    var dir = Flow[x, y];
                    var target = new int2(x, y) + new int2((int)dir.x, (int)dir.y);
                    Targets[x, y] = Targets[target.x, target.y];
                    return r1;
                }

                var result = (minX + minY + (float)math.sqrt((minX + minY) * (minX + minY) - 2f * (minX * minX + minY * minY - 1.0 / (f * f)))) * 0.5f;

                var vec = new float2(minX - result, minY - result);
                if (math.abs(Distances[x - 1, y]) > math.abs(Distances[x + 1, y]))
                    vec.x *= -1;
                if (math.abs(Distances[x, y - 1]) > math.abs(Distances[x, y + 1]))
                    vec.y *= -1;

                Flow[x, y] = math.normalize(vec);
                Targets[x, y] = intersectionPoint;

                return result;
            }
        }

        private bool TryGetNeighbor(int2 pos, int index, out int2 neighbor) {
            neighbor = pos;
            neighbor.x += ShiftX(index);
            neighbor.y += ShiftY(index);
            return IsIn(neighbor);
        }

        private static int ShiftX(int index) {
            return index switch {
                0 => 0,
                1 => 1,
                2 => 0,
                3 => -1,
                _ => 0
            };
        }

        private static int ShiftY(int index) {
            return index switch {
                0 => 1,
                1 => 0,
                2 => -1,
                3 => 0,
                _ => 0
            };
        }

        private int GetIndex(int2 pos) {
            return pos.y * (Size.x + 2) + pos.x;
        }

        private int GetIndex(int x, int y) {
            return y * (Size.x + 2) + x;
        }

        private static bool TryIntersectRays(float2 rayOrigin1, float2 rayDirection1, float2 rayOrigin2,
            float2 rayDirection2, out float2 intersectionPoint) {
            intersectionPoint = float2.zero;

            var crossProduct = rayDirection1.x * rayDirection2.y - rayDirection1.y * rayDirection2.x;

            if (crossProduct == 0) return false;

            var diff = rayOrigin2 - rayOrigin1;
            var t = (diff.x * rayDirection2.y - diff.y * rayDirection2.x) / crossProduct;
            var u = (diff.x * rayDirection1.y - diff.y * rayDirection1.x) / crossProduct;

            if (t < 0 || u < 0) return false;

            intersectionPoint = rayOrigin1 + t * rayDirection1;
            return true;
        }

        private bool IsIn(int2 pos) {
            return 1 <= pos.x && pos.x <= Size.x && 1 <= pos.y && pos.y <= Size.y;
        }

    }

}