using Unity.Mathematics;

namespace FlowTiles.Examples {

    public struct SpawnAgentCommand {
        public int2 Cell;
        public AgentType Type;
    }

    public enum AgentType { SINGLE, STRESS_TEST }

}