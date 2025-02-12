using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct FindSightlinesJob : IJobFor {

        public struct Task {

            [ReadOnly] public int4 CacheKey;
            [ReadOnly] public int2 StartCell;
            [ReadOnly] public int2 EndCell;
            [ReadOnly] public int TravelType;

            public UnsafeArray<bool> SightlineExists; // Expecting size = 1

            public void DisposeTempData() {
                SightlineExists.Dispose();
            }

            public void Dispose() {
                SightlineExists.Dispose();
            }

        }

        public NativeArray<Task> Tasks;
        [ReadOnly] public PathableGraph Graph;

        public FindSightlinesJob(NativeArray<Task> tasks, PathableGraph graph) {
            Tasks = tasks;
            Graph = graph;
        }

        public void Execute(int index) {
            var task = Tasks[index];
            var result = FlowTileUtils.HasLineOfSight(
                task.StartCell,
                task.EndCell,
                ref Graph,
                task.TravelType,
                precise: true);

            task.SightlineExists[0] = result;
        }

    }

}