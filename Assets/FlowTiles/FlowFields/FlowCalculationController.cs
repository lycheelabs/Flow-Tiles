using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(CostSector sector, CellRect goalBounds, int2 exitDirection) {
            var sectorBounds = sector.Bounds;
            var size = sectorBounds.SizeCells;
            var w = size.x;
            var h = size.y;

            var goalMin = goalBounds.MinCell - sectorBounds.MinCell;
            var goalMax = goalBounds.MaxCell - sectorBounds.MinCell;
            var goalW = goalMax.x - goalMin.x + 1;
            var goalH = goalMax.y - goalMin.y + 1;
            var numGoals = goalW * goalH;
            var goalColor = sector.Colors[goalMin.x, goalMin.y];

            // Initialise flow speeds
            var numFlowCells = (w + 2) * (h + 2);
            var speedData = new NativeArray<float>(numFlowCells, Allocator.TempJob);
            for (var x = 1; x <= w; x++) {
                for (var y = 1; y <= h; y++) {
                    var cost = sector.Costs[x - 1, y - 1];
                    var color = sector.Colors[x - 1, y - 1];
                    var index = y * (w + 2) + x;
                    speedData[index] = 1f / cost;
                    if (cost == PathableLevel.WALL_COST || color != goalColor) speedData[index] = -1;
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

            // Initialise the job
            var flow = new UnsafeField<float2>(size + 2, Allocator.Persistent);
            var job = new FlowCalculationJob() {
                Size = size,
                Speeds = speedData,
                Goals = goalData,
                ExitDirection = exitDirection,
                Flow = flow,
            };

            // Execute and time the job
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var handle = job.Schedule();
            handle.Complete();
            stopwatch.Stop();

            // Dispose of temporary data
            speedData.Dispose();
            goalData.Dispose();

            return new FlowFieldTile {
                Size = size,
                Directions = flow,
                GenerationTime = stopwatch.Elapsed,
            };
        }

    }

}