using FlowTiles.PortalGraphs;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(CostField costs, Vector2Int[] goal) {
            var width = costs.size.x;
            var height = costs.size.y;

            var size = (width + 2) * (height + 2);
            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 1; x <= width; x++) {
                for (var y = 1; y <= height; y++) {
                    speedData[y * (width + 2) + x] = 1;// 1f / costs.GetCost(x-1, y-1);
                }
            }

            for (var x = 1; x <= width; x++) {
                for (var y = 1; y <= height; y++) {
                    var cost = costs.GetCost(x - 1, y - 1);
                    var index = y * (width + 2) + x;
                    speedData[index] = 1f / cost;
                    if (cost == CostField.WALL) speedData[index] = -1;
                }
            }

            return RequestCalculation(speedData, goal, width, height);
        }

        public static FlowFieldTile RequestCalculation(float[,] speeds, Vector2Int[] goal, int width, int height) {
            var size = (width + 2) * (height + 2);
            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 0; x <= width + 1; x++) {
                for (var y = 0; y <= height + 1; y++) {
                    speedData[y * (width + 2) + x] = speeds[x, y];
                }
            }

            return RequestCalculation(speedData, goal, width, height);
        }

        public static FlowFieldTile RequestCalculation(NativeArray<double> speedData, Vector2Int[] goal, int width, int height) {
            var size = (width + 2) * (height + 2);

            var directions = new NativeArray<double2>(size, Allocator.Persistent);
            var distances = new NativeArray<double>(size, Allocator.Persistent);
            var targets = new NativeArray<double2>(size, Allocator.Persistent);

            var goalData = new NativeArray<Vector2Int>(goal.Length, Allocator.Persistent);
            for (var i = 0; i < goal.Length; i++) {
                goalData[i] = goal[i];
            }

            var job = new FlowCalculationJob() {
                Speeds = speedData,
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