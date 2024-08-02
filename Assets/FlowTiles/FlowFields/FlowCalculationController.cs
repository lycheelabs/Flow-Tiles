using FlowTiles.PortalGraphs;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.FlowField {

    public static class FlowCalculationController {

        public static FlowFieldTile RequestCalculation(CostSector sector, CellRect goalBounds, int2 exitDirection) {
            var calculator = new FlowCalculator(sector, goalBounds, exitDirection);
            var job = new FlowFieldJob {
                Calculator = calculator,
            };

            // Execute and time the job
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var handle = job.Schedule();
            handle.Complete();
            stopwatch.Stop();

            return new FlowFieldTile {
                SectorIndex = sector.Index,
                Color = calculator.Color,
                Size = calculator.Size,
                Directions = calculator.Flow,
                GenerationTime = stopwatch.Elapsed,
            };
        }

    }

}