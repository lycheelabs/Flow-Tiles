using FlowTiles.Utils;
using Unity.Burst;
using Unity.Jobs;

namespace FlowTiles.FlowField {

    [BurstCompile]
    public struct FlowCalculationJob : IJob {

        public FlowCalculator Calculator;

        public void Execute() {
            Calculator.Calculate();
        }

    }

}