using FlowTiles.PortalGraphs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowTiles {

    [BurstCompile]
    public struct PortalPathJob : IJob {

        public static bool ScheduleAndComplete (PortalPathfinder pathfinder, int2 start, int2 dest, out UnsafeList<PortalPathNode> result) {
            var job = new PortalPathJob {
                Pathfinder = pathfinder, 
                Start = start, 
                Dest = dest 
            };
            job.Schedule().Complete();
            result = job.Result;
            return job.Success;
        }

        // ------------------------------------------------

        public PortalPathfinder Pathfinder;
        public int2 Start;
        public int2 Dest;

        public UnsafeList<PortalPathNode> Result => Pathfinder.Result;
        public bool Success;

        public void Execute() {
            Success = Pathfinder.TryFindPath(Start, Dest);
        }


    }

}