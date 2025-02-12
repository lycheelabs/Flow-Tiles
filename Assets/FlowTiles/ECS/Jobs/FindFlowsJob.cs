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

            public int IslandIndex;
            public UnsafeField<float2> Flow;
            public UnsafeField<int> Distances;

            public FlowField ResultAsFlowField() {
                var goalCell = GoalBounds.CentreCell - Sector.Bounds.MinCell;
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

            public void DisposeTempData() {
                //
            }

            public void Dispose() {
                Flow.Dispose();
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
            var distance = task.Distances;
            calculator.Calculate(ref flow, ref distance);
            task.Flow = flow;
        }

    }

}