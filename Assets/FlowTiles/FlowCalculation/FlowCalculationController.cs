using FlowTiles.PortalGraphs;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(CostField costs, Vector2Int[] goal, int2 exitDirection) {
            var w = costs.size.x;
            var h = costs.size.y;
            var size = (w + 2) * (h + 2);

            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 1; x <= w; x++) {
                for (var y = 1; y <= h; y++) {
                    var cost = costs.GetCost(x - 1, y - 1);
                    var index = y * (w + 2) + x;
                    speedData[index] = 1f / cost;
                    if (cost == CostField.WALL) speedData[index] = -1;
                }
            }

            return RequestCalculation(costs.size, speedData, goal, exitDirection);
        }

        public static FlowFieldTile RequestCalculation(float[,] speeds, Vector2Int[] goal, int width, int height) {
            var size = (width + 2) * (height + 2);
            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 0; x <= width + 1; x++) {
                for (var y = 0; y <= height + 1; y++) {
                    speedData[y * (width + 2) + x] = speeds[x, y];
                }
            }

            return RequestCalculation(new int2(width, height), speedData, goal, 0);
        }

        public static FlowFieldTile RequestCalculation(int2 size, NativeArray<double> speedData, Vector2Int[] goals, int2 exitDirection) {
            var totalCells = (size.x + 2) * (size.y + 2);

            var directions = new NativeArray<double2>(totalCells, Allocator.Persistent);
            var distances = new NativeArray<double>(totalCells, Allocator.Persistent);
            var targets = new NativeArray<double2>(totalCells, Allocator.Persistent);

            var goalData = new NativeArray<Vector2Int>(goals.Length, Allocator.Persistent);
            for (var i = 0; i < goals.Length; i++) {
                goalData[i] = goals[i];
            }

            var job = new FlowCalculationJob() {
                Size = size,
                Speeds = speedData,
                Goals = goalData,
                ExitDirection = exitDirection,

                Directions = directions,
                Distances = distances,
                Targets = targets
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
                Size = size,
                Directions = directions,
                Gradients = distances,
                GenerationTime = stopwatch.Elapsed,
            };
        }

    }

}