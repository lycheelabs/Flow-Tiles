using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.FlowField {
    [BurstCompile]
    public struct FlowCalculationJob : IJob {

        [ReadOnly] public int2 Size;
        [ReadOnly] public NativeArray<double> Speeds;
        [ReadOnly] public NativeArray<Vector2Int> Goals;
        [ReadOnly] public float2 ExitDirection;

        // Direction of the flow.
        public NativeArray<double2> Directions;
        // Distance from the closest goal.
        public NativeArray<double> Distances;
        // Position towards which the flow is directed (approximately).
        public NativeArray<double2> Targets;

        private static double Inf => 1000000000;
        private static double ObstacleStep => 10000;
        private static double ObstacleOverrideValue => 1;

        private const int NeighborsCount = 4;

        public void Execute() {

            var queue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var secondQueue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var thirdQueue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var processedPositions = new NativeArray<bool>((Size.x + 2) * (Size.y + 2), Allocator.Temp);

            for (var x = 0; x < Size.x + 2; x++) {
                for (var y = 0; y < Size.y + 2; y++) {
                    Distances[GetIndex(x, y)] = Inf;
                }
            }

            // Initialise the goal wavefront
            var sourcesCount = 0;

            for (int s = 0; s < Goals.Length; s++) {
                var source = Goals[s] + new Vector2Int(1, 1);
                var index = GetIndex(source.x, source.y);

                Distances[index] = 0;
                Targets[index] = new double2(source.x, source.y);
                Directions[index] = ExitDirection;
                processedPositions[index] = true;

                //queue.Enqueue(source);
                var sourceSpeed = Speeds[GetIndex(source)];
                for (var i = 0; i < NeighborsCount; i++) {
                    if (TryGetNeighbor(source, i, out var neighbor) && !Goals.Contains(neighbor - new Vector2Int(1, 1))) {
                        if (sourceSpeed == 0) sourcesCount++;
                        var neighborSpeed = Speeds[GetIndex(neighbor)];
                        var q = Speeds[GetIndex(neighbor)] > 0 ? queue : secondQueue;
                        q.Enqueue(neighbor);
                        processedPositions[GetIndex(neighbor)] = true;
                    }
                }

            }

            const int iterationLimit = 500000;
            var iterationsCount = 0;

            // Iterate the wavefront expansion
            while ((queue.Count > 0 || secondQueue.Count > 0) && iterationsCount < iterationLimit) {
                while (queue.Count > 0 && iterationsCount++ < iterationLimit) {
                    var current = queue.Dequeue();
                    Distances[GetIndex(current)] = UpdatePoint(current.x, current.y);

                    if (sourcesCount > 0) {
                        sourcesCount--;
                        Directions[GetIndex(current)] = double2.zero;
                        Targets[GetIndex(current)] = new double2(current.x, current.y);
                    }

                    for (var i = 0; i < NeighborsCount; i++) {
                        if (TryGetNeighbor(current, i, out var neighbor) &&
                            processedPositions[GetIndex(neighbor)] == false) {
                            var q = Speeds[GetIndex(neighbor)] > 0f ? queue : secondQueue;
                            processedPositions[GetIndex(neighbor)] = true;
                            q.Enqueue(neighbor);
                        }
                    }
                }

                while (secondQueue.Count > 0 && iterationsCount++ < iterationLimit) {
                    var current = secondQueue.Dequeue();
                    Distances[GetIndex(current)] = UpdatePoint(current.x, current.y) + ObstacleStep;
                    for (var i = 0; i < NeighborsCount; i++) {
                        if (TryGetNeighbor(current, i, out var neighbor) &&
                            processedPositions[GetIndex(neighbor)] == false) {
                            var q = Speeds[GetIndex(neighbor)] > 0f ? queue : thirdQueue;
                            processedPositions[GetIndex(neighbor)] = true;
                            q.Enqueue(neighbor);
                        }
                    }
                }

                while (thirdQueue.Count > 0) secondQueue.Enqueue(thirdQueue.Dequeue());

                sourcesCount = queue.Count;
            }
        }

        private double UpdatePoint(int x, int y) {
            var xyIndex = GetIndex(x, y);
            var f = Speeds[xyIndex];

            if (f == 0) f = ObstacleOverrideValue;

            var minX = math.min(Distances[GetIndex(x - 1, y)], Distances[GetIndex(x + 1, y)]);
            var minY = math.min(Distances[GetIndex(x, y - 1)], Distances[GetIndex(x, y + 1)]);

            if (math.abs(minX - minY) >= 1 / f) {
                var result = math.min(minX + 1 / f, minY + 1 / f);
                if (minX < minY) {
                    Directions[xyIndex] = Distances[GetIndex(x - 1, y)] < Distances[GetIndex(x + 1, y)]
                        ? new double2(-1, 0)
                        : new double2(1, 0);
                }
                else {
                    Directions[xyIndex] = Distances[GetIndex(x, y - 1)] < Distances[GetIndex(x, y + 1)]
                        ? new double2(0, -1)
                        : new double2(0, 1);
                }

                var dir = Directions[xyIndex];
                var prevIndex = GetIndex(new Vector2Int(x, y) + new Vector2Int((int)dir.x, (int)dir.y));

                if (Directions[xyIndex].Equals(Directions[prevIndex])) {
                    Targets[xyIndex] = Targets[prevIndex];
                }
                else {
                    Targets[xyIndex] = new double2(x, y) + dir;
                }

                return result;
            }
            else {
                var usedX = Distances[GetIndex(x - 1, y)] < Distances[GetIndex(x + 1, y)] ? x - 1 : x + 1;
                var usedY = Distances[GetIndex(x, y - 1)] < Distances[GetIndex(x, y + 1)] ? y - 1 : y + 1;

                var directionX = Directions[GetIndex(usedX, y)];
                var directionY = Directions[GetIndex(x, usedY)];

                if (TryIntersectRays(new double2(usedX, y), directionX, new double2(x, usedY),
                        directionY, out var intersectionPoint) == false) {
                    var r1 = math.min(minX + 1 / f, minY + 1 / f);
                    if (minX < minY) {
                        Directions[xyIndex] = Distances[GetIndex(x - 1, y)] < Distances[GetIndex(x + 1, y)]
                            ? new double2(-1, 0)
                            : new double2(1, 0);
                    }
                    else {
                        Directions[xyIndex] = Distances[GetIndex(x, y - 1)] < Distances[GetIndex(x, y + 1)]
                            ? new double2(0, -1)
                            : new double2(0, 1);
                    }

                    var dir = Directions[xyIndex];
                    Targets[xyIndex] = Targets[GetIndex(new Vector2Int(x, y) + new Vector2Int((int)dir.x, (int)dir.y))];
                    return r1;
                }

                var result = (minX + minY + math.sqrt((minX + minY) * (minX + minY) - 2 * (minX * minX + minY * minY - 1.0 / (f * f)))) * 0.5;

                var vec = new double2(minX - result, minY - result);
                if (math.abs(Distances[GetIndex(x - 1, y)]) > math.abs(Distances[GetIndex(x + 1, y)]))
                    vec.x *= -1;
                if (math.abs(Distances[GetIndex(x, y - 1)]) > math.abs(Distances[GetIndex(x, y + 1)]))
                    vec.y *= -1;

                Directions[xyIndex] = math.normalize(vec);
                Targets[xyIndex] = intersectionPoint;

                return result;
            }
        }

        private bool TryGetNeighbor(Vector2Int pos, int index, out Vector2Int neighbor) {
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

        private int GetIndex(Vector2Int pos) {
            return pos.y * (Size.x + 2) + pos.x;
        }

        private int GetIndex(int x, int y) {
            return y * (Size.x + 2) + x;
        }

        private static bool TryIntersectRays(double2 rayOrigin1, double2 rayDirection1, double2 rayOrigin2,
            double2 rayDirection2, out double2 intersectionPoint) {
            intersectionPoint = double2.zero;

            var crossProduct = rayDirection1.x * rayDirection2.y - rayDirection1.y * rayDirection2.x;

            if (crossProduct == 0) return false;

            var diff = rayOrigin2 - rayOrigin1;
            var t = (diff.x * rayDirection2.y - diff.y * rayDirection2.x) / crossProduct;
            var u = (diff.x * rayDirection1.y - diff.y * rayDirection1.x) / crossProduct;

            if (t < 0 || u < 0) return false;

            intersectionPoint = rayOrigin1 + t * rayDirection1;
            return true;
        }

        private bool IsIn(Vector2Int pos) {
            return 1 <= pos.x && pos.x <= Size.x && 1 <= pos.y && pos.y <= Size.y;
        }

    }
}