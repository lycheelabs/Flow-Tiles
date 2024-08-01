using FlowTiles.Utils;
using System.Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace FlowTiles.FlowField {

    [BurstCompile]
    public struct FlowCalculationJob : IJob {

        [ReadOnly] public int2 Size;
        [ReadOnly] public NativeArray<float> Speeds;
        [ReadOnly] public NativeArray<Vector2Int> Goals;
        [ReadOnly] public float2 ExitDirection;

        // Distance from the closest goal.
        public UnsafeField<float> Distances;
        // Position towards which the flow is directed (approximately).
        public UnsafeField<float2> Targets;
        // Direction of the flow.
        public UnsafeField<float2> Flow;

        private static float Inf => 1000000000;
        private static float ObstacleStep => 10000;
        private static float ObstacleOverrideValue => 1;

        private const int NeighborsCount = 4;

        public void Execute() {
            Targets = new UnsafeField<float2>(Size + 2, Allocator.TempJob);
            Distances = new UnsafeField<float>(Size + 2, Allocator.TempJob);

            var queue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var secondQueue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var thirdQueue = new NativeQueue<Vector2Int>(Allocator.Temp);
            var processedPositions = new NativeArray<bool>((Size.x + 2) * (Size.y + 2), Allocator.Temp);

            for (var x = 0; x < Size.x + 2; x++) {
                for (var y = 0; y < Size.y + 2; y++) {
                    Distances[x, y] = Inf;
                }
            }

            // Initialise the goal wavefront
            var sourcesCount = 0;

            for (int s = 0; s < Goals.Length; s++) {
                var source = Goals[s] + new Vector2Int(1, 1);
                var index = GetIndex(source.x, source.y);

                Distances[source.x, source.y] = 0;
                Targets[source.x, source.y] = new float2(source.x, source.y);
                Flow[source.x, source.y] = ExitDirection;
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
                    Distances[current.x, current.y] = UpdatePoint(current.x, current.y);

                    if (sourcesCount > 0) {
                        sourcesCount--;
                        Flow[current.x, current.y] = float2.zero;
                        Targets[current.x, current.y] = new float2(current.x, current.y);
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
                    Distances[current.x, current.y] = UpdatePoint(current.x, current.y) + ObstacleStep;
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

            Targets.Dispose();
            Distances.Dispose();
        }

        private float UpdatePoint(int x, int y) {
            var xyIndex = GetIndex(x, y);
            var f = Speeds[xyIndex];

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
                var prev = new Vector2Int(x, y) + new Vector2Int((int)dir.x, (int)dir.y);

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
                    var target = new Vector2Int(x, y) + new Vector2Int((int)dir.x, (int)dir.y);
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

        private bool IsIn(Vector2Int pos) {
            return 1 <= pos.x && pos.x <= Size.x && 1 <= pos.y && pos.y <= Size.y;
        }

    }

}