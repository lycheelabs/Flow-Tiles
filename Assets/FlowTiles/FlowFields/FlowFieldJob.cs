using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.FlowField {

    [BurstCompile]
    public struct FlowFieldJob : IJob {

        public static FlowFieldTile ScheduleAndComplete(CostSector sector, CellRect goalBounds, int2 exitDirection) {
            var job = new FlowFieldJob(sector, goalBounds, exitDirection);

            // Execute and time the job
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            job.Schedule().Complete();
            stopwatch.Stop();

            return new FlowFieldTile {
                SectorIndex = sector.Index,
                Color = job.Color.Value,
                Size = sector.Bounds.SizeCells,
                Directions = job.Flow.Value,
                GenerationTime = stopwatch.Elapsed,
            };
        }

        // --------------------------------------------------------

        [ReadOnly] public CostSector Sector;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public int2 ExitDirection;

        // Result
        public NativeReference<UnsafeField<float2>> Flow;
        public NativeReference<short> Color;

        public FlowFieldJob (CostSector sector, CellRect goalBounds, int2 exitDirection) {
            Sector = sector;
            GoalBounds = goalBounds;
            ExitDirection = exitDirection;
            Flow = new NativeReference<UnsafeField<float2>>(Allocator.TempJob);
            Color = new NativeReference<short>(Allocator.TempJob);
        }

        public void Execute() {
            var calculator = new FlowCalculator(Sector, GoalBounds, ExitDirection);
            calculator.Calculate();
            Flow.Value = calculator.Flow;
            Color.Value = calculator.Color;
        }

    }

}