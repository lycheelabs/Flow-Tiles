using FlowTiles.PortalPaths;
using System;
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
            [ReadOnly] public int TravelType;

            public bool Success;
            public UnsafeList<PortalPathNode> Path;

            public void Dispose() {
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

            task.Success = pathfinder.TryFindPath(task.Start, task.Dest, task.TravelType, ref path);
            task.Path = path;
            Tasks[index] = task;
        }

    }

}