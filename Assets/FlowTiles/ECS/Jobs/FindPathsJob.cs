using FlowTiles.FlowFields;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct FindPathsJob : IJobFor {

        public struct Task {
            [ReadOnly] public int4 CacheKey;
            [ReadOnly] public int2 Start;
            [ReadOnly] public int2 Dest;
            [ReadOnly] public FlowField StartField;
            [ReadOnly] public FlowField DestField;
            [ReadOnly] public int TravelType;

            public UnsafeArray<bool> Success; // Expecting size = 1
            public UnsafeList<PortalPathNode> Path;

            public void Dispose() {
                Success.Dispose();
                Path.Dispose();
            }
        }

        [ReadOnly] public PathableGraph Graph;
        public NativeArray<Task> Tasks;

        public FindPathsJob (PathableGraph graph, NativeArray<Task> tasks) {
            Graph = graph;
            Tasks = tasks;
        }

        [BurstCompile]
        public void Execute(int index) {
            var task = Tasks[index];
            var pathfinder = new PortalPathfinder(Graph, Constants.EXPECTED_MAX_SEARCHED_NODES, Allocator.Temp);
            var path = task.Path;

            var success = pathfinder.TryFindPath(task.Start, task.StartField, task.Dest, task.DestField, task.TravelType, ref path);
            task.Path = path;
            task.Success[0] = success;
            Tasks[index] = task;
        }

    }

}