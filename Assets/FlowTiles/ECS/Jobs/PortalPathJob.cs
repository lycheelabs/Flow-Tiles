using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct PortalPathJob : IJobFor {

        public struct Task {
            public int4 CacheKey;
            public int2 Start;
            public int2 Dest;
            public int TravelType;

            public bool Success;
            public UnsafeList<PortalPathNode> Path;
        }

        [ReadOnly] public PathableGraph Graph;
        public NativeArray<Task> Tasks;

        public PortalPathJob (PathableGraph graph, NativeArray<Task> tasks) {
            Graph = graph;
            Tasks = tasks;
        }

        [BurstCompile]
        public void Execute(int index) {
            var task = Tasks[index];
            var pathfinder = new PortalPathfinder(Graph);
            var path = task.Path;

            task.Success = pathfinder.TryFindPath(task.Start, task.Dest, task.TravelType, ref path);
            task.Path = path;
            Tasks[index] = task;
        }

    }

}