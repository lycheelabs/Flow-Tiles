﻿using FlowTiles.FlowFields;
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
            var job = new CalculateFlowJob(map.Costs, goalBounds, exitDirection);
            job.Schedule().Complete();

            var result = new FlowField {
                SectorIndex = map.Index,
                Color = job.Color.Value,
                Size = map.Bounds.SizeCells,
                Directions = job.Flow.Value,
                Version = map.Version,
            };

            job.Dispose();
            return result;
        }

        // --------------------------------------------------------

        [ReadOnly] public CostMap Sector;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public int2 ExitDirection;

        // Result
        public NativeReference<UnsafeField<float2>> Flow;
        public NativeReference<short> Color;

        public CalculateFlowJob (CostMap sector, CellRect goalBounds, int2 exitDirection) {
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