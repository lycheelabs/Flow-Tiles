using FlowTiles.PortalGraphs;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using FlowTiles;

namespace FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(Sector sector, Boundaries goalBounds, int2 exitDirection) {
            var sectorBounds = sector.Bounds;
            var size = sectorBounds.Size;
            var w = size.x;
            var h = size.y;

            var goalMin = goalBounds.Min - sectorBounds.Min;
            var goalMax = goalBounds.Max - sectorBounds.Min;
            var goalW = goalMax.x - goalMin.x + 1;
            var goalH = goalMax.y - goalMin.y + 1;
            var numGoals = goalW * goalH;
            var goalColor = sector.Colors.GetColor(goalMin);

            // Initialise flow speeds
            var numFlowCells = (w + 2) * (h + 2);
            var speedData = new NativeArray<double>(numFlowCells, Allocator.TempJob);
            for (var x = 1; x <= w; x++) {
                for (var y = 1; y <= h; y++) {
                    var cost = sector.Costs.GetCost(x - 1, y - 1);
                    var color = sector.Colors.GetColor(x - 1, y - 1);
                    var index = y * (w + 2) + x;
                    speedData[index] = 1f / cost;
                    if (cost == CostField.WALL || color != goalColor) speedData[index] = -1;
                }
            }

            // Initialise goal cells
            var goalData = new NativeArray<Vector2Int>(numGoals, Allocator.TempJob);
            int goal = 0;
            for (int x = goalMin.x; x <= goalMax.x; x++) {
                for (int y = goalMin.y; y <= goalMax.y; y++) {
                    goalData[goal] = new Vector2Int(x, y);
                    goal++;
                }
            }

            return RequestCalculation(size, speedData, goalData, exitDirection);
        }

        public static FlowFieldTile RequestCalculation(float[,] speeds, Vector2Int[] goals, int width, int height) {
            var size = (width + 2) * (height + 2);
            var speedData = new NativeArray<double>(size, Allocator.TempJob);
            for (var x = 0; x <= width + 1; x++) {
                for (var y = 0; y <= height + 1; y++) {
                    speedData[y * (width + 2) + x] = speeds[x, y];
                }
            }

            var goalData = new NativeArray<Vector2Int>(goals.Length, Allocator.Persistent);
            for (var i = 0; i < goals.Length; i++) {
                goalData[i] = goals[i];
            }

            return RequestCalculation(new int2(width, height), speedData, goalData, 0);
        }

        public static FlowFieldTile RequestCalculation(int2 size, NativeArray<double> speedData, NativeArray<Vector2Int> goalData, int2 exitDirection) {
            var totalCells = (size.x + 2) * (size.y + 2);
            var directions = new NativeArray<double2>(totalCells, Allocator.Persistent);
            var distances = new NativeArray<double>(totalCells, Allocator.Persistent);
            var targets = new NativeArray<double2>(totalCells, Allocator.Persistent);

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