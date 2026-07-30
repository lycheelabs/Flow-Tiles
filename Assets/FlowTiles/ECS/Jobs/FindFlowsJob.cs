using LycheeLabs.FlowTiles.FlowFields;
using LycheeLabs.FlowTiles.PortalPaths;
using LycheeLabs.FlowTiles.Utils;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.ECS {

    [BurstCompile]
    public struct FindFlowsJob : IJobFor {

        public struct Task {

            [ReadOnly] public int4 CacheKey;
            [ReadOnly] public SectorData Sector;
            [ReadOnly] public CellRect GoalBounds;
            [ReadOnly] public int2 ExitDirection;

            public int IslandIndex;
            public UnsafeField<float2> Flow;
            public UnsafeField<int> Distances;

            public FlowField ResultAsFlowField() {
                var goalCell = GoalBounds.CentreCell - Sector.Bounds.MinCell;
                if (!Sector.Islands.Cells.IsValidIndex(goalCell.x, goalCell.y)) {
                    throw new InvalidOperationException($"Goal cell {goalCell} is outside sector island data bounds {Sector.Islands.Cells.Size}");
                }
                var goalIsland = Sector.Islands.Cells[goalCell.x, goalCell.y];

                return new FlowField {
                    SectorIndex = Sector.Index,
                    IslandIndex = goalIsland,
                    Version = Sector.Version,
                    Directions = Flow,
                    Distances = Distances,
                    Size = Sector.Bounds.SizeCells,
                    Corner = Sector.Bounds.MinCell,
                };
            }

            public void Dispose () {
                // Nothing to dispose at this step
            }

            // Also disposes data intended for caching
            public void DisposeAll () {
                if (Flow.IsCreated) Flow.Dispose();
                if (Distances.IsCreated) Distances.Dispose();
            }

        }

        public NativeArray<Task> Tasks;

        public FindFlowsJob (NativeArray<Task> tasks) {
            Tasks = tasks;
        }

        [BurstCompile]
        public void Execute(int index) {
            var task = Tasks[index];
            var calculator = new FlowCalculator(task, Allocator.Temp);

            var flow = task.Flow;
            var distance = task.Distances;
            calculator.Calculate(ref flow, ref distance, true);
            task.Flow = flow;
            task.Distances = distance;
            calculator.Dispose();
            Tasks[index] = task;
        }

    }

}