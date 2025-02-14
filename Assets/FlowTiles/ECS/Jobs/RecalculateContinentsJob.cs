using FlowTiles.PortalPaths;
using Unity.Burst;
using Unity.Jobs;

namespace FlowTiles.ECS {

    [BurstCompile]
    public struct RecalculateContinentsJob : IJob {

        public PathableGraph Graph;
        public ContinentPathfinder Pathfinder;

        public RecalculateContinentsJob(PathableGraph graph, ContinentPathfinder pathfinder) : this() {
            Graph = graph;
            Pathfinder = pathfinder;
        }

        [BurstCompile]
        public void Execute() {
            Pathfinder.RecalculateContinents(ref Graph);
        }

    }

}