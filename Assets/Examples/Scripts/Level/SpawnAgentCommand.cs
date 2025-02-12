using FlowTiles.ECS;
using Unity.Mathematics;

namespace FlowTiles.Examples {

    public struct SpawnAgentCommand {
        public int2 Cell;
        public int TravelType;
        public AgentType Type;
        public PathSmoothingMode LOSMode;
    }

    public enum AgentType { SINGLE, MULTIPLE, STRESS_TEST }

}