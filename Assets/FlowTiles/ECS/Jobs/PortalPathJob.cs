using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct PortalPathJob : IJob {

        public static bool ScheduleAndComplete (PathableGraph graph, int2 start, int2 dest, int travelType, out UnsafeList<PortalPathNode> result) {
            var job = new PortalPathJob(graph, start, dest, travelType);
            job.Schedule().Complete();
            
            result = job.Result.Value;
            var success = job.Success.Value;
            
            job.Dispose();
            return success;
        }

        // ------------------------------------------------

        public PathableGraph Graph;
        public int2 Start;
        public int2 Dest;
        public int TravelType;

        public NativeReference<UnsafeList<PortalPathNode>> Result;
        public NativeReference<bool> Success;

        public PortalPathJob (PathableGraph graph, int2 start, int2 dest, int travelType) {
            Graph = graph;
            Start = start;
            Dest = dest;
            TravelType = travelType;

            Result = new NativeReference<UnsafeList<PortalPathNode>>(Allocator.TempJob);
            Success = new NativeReference<bool>(Allocator.TempJob);

            Result.Value = new UnsafeList<PortalPathNode>(32, Allocator.Persistent);
            Success.Value = false;
        }

        [BurstCompile]
        public void Execute() {
            var pathfinder = new PortalPathfinder(Graph);
            var path = Result.Value;
            Success.Value = pathfinder.TryFindPath(Start, Dest, TravelType, ref path);
            Result.Value = path;
        }

        public void Dispose() {
            Success.Dispose();
            Result.Dispose();
        }

    }

}