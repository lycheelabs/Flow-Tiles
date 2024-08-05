using FlowTiles.FlowFields;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct FlowFieldJob : IJob {

        public static FlowField ScheduleAndComplete(CostSector sector, CellRect goalBounds, int2 exitDirection) {
            var job = new FlowFieldJob(sector, goalBounds, exitDirection);
            job.Schedule().Complete();

            var result = new FlowField {
                SectorIndex = sector.Index,
                Color = job.Color.Value,
                Size = sector.Bounds.SizeCells,
                Directions = job.Flow.Value,
            };

            job.Dispose();
            return result;
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

        public void Dispose () {
            Flow.Dispose();
            Color.Dispose();
        }

    }

}