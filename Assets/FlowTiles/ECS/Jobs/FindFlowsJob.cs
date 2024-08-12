using System;
using FlowTiles.FlowFields;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct FindFlowsJob : IJobFor {

        public struct Task {
            [ReadOnly] public int4 CacheKey;
            [ReadOnly] public SectorMap Sector;
            [ReadOnly] public CellRect GoalBounds;
            [ReadOnly] public int2 ExitDirection;

            public int Color;
            public UnsafeField<float2> Flow;

            public FlowField ResultAsFlowField() {
                return new FlowField {
                    SectorIndex = Sector.Index,
                    Version = Sector.Version,
                    Directions = Flow,
                    Color = (short)Color,
                    Size = Sector.Bounds.SizeCells,
                };
            }

        }

        public NativeArray<Task> Tasks;

        public FindFlowsJob (NativeArray<Task> tasks) {
            Tasks = tasks;
        }

        public void Execute(int index) {
            var task = Tasks[index];
            var calculator = new FlowCalculator(task, Allocator.Temp);
            var flow = task.Flow;

            calculator.Calculate(ref flow);
            task.Flow = flow;
            task.Color = calculator.Color;
        }

    }

}