using Unity.Burst;
using Unity.Jobs;

namespace FlowTiles.FlowField {

    [BurstCompile]
    public struct FlowFieldJob : IJob {

        public FlowCalculator Calculator;

        public void Execute() {
            Calculator.Calculate();
        }

    }

}