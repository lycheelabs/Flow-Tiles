using FlowTiles.FlowFields;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct CalculateFlowJob : IJob {

        public static FlowField ScheduleAndComplete(SectorMap map, CellRect goalBounds, int2 exitDirection) {
            var job = new CalculateFlowJob(map, goalBounds, exitDirection);
            job.Schedule().Complete();

            var result = new FlowField {
                SectorIndex = map.Index,
                Version = map.Version,
                Color = job.Color.Value,
                Size = map.Bounds.SizeCells,
                Directions = job.Flow.Value,
            };

            job.Dispose();
            return result;
        }

        // --------------------------------------------------------

        [ReadOnly] public SectorMap Sector;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public int2 ExitDirection;

        // Result
        public NativeReference<UnsafeField<float2>> Flow;
        public NativeReference<short> Color;

        public CalculateFlowJob (SectorMap sector, CellRect goalBounds, int2 exitDirection) {
            Sector = sector;
            GoalBounds = goalBounds;
            ExitDirection = exitDirection;
            Flow = new NativeReference<UnsafeField<float2>>(Allocator.TempJob);
            Color = new NativeReference<short>(Allocator.TempJob);
        }

        public void Execute() {
            var calculator = new FlowCalculator(Sector.Costs, Sector.Colors, GoalBounds, ExitDirection, Allocator.Temp);
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