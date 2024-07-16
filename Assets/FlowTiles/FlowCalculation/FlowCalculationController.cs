using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(float[,] speeds, Vector2Int[] goal, int width, int height) {
            var size = (width + 2) * (height + 2);
            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 0; x <= width + 1; x++) {
                for (var y = 0; y <= height + 1; y++) {
                    speedData[y * (width + 2) + x] = speeds[x, y];
                }
            }

            var directions = new NativeArray<double2>(size, Allocator.Persistent);
            var distances = new NativeArray<double>(size, Allocator.Persistent);
            var targets = new NativeArray<double2>(size, Allocator.Persistent);

            var goalData = new NativeArray<Vector2Int>(goal.Length, Allocator.Persistent);
            for (var i = 0; i < goal.Length; i++) {
                goalData[i] = goal[i];
            }

            var job = new FlowCalculationJob() {
                Map = speedData,
                Goals = goalData,
                Height = height,
                Width = width,

                Direction = directions,
                Distance = distances,
                Target = targets
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var handle = job.Schedule();
            handle.Complete();

            stopwatch.Stop();

            targets.Dispose();
            speedData.Dispose();
            goalData.Dispose();

            return new FlowFieldTile {
                Size = new int2(width, height),
                Directions = directions,
                Gradients = distances,
                GenerationTime = stopwatch.Elapsed,
            };
        }

    }

}