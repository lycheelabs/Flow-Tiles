using FlowTiles.ECS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class TerrainExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;
        public bool VisualiseConnections;

        private DemoLevel Level;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution, 2, 2);
            map.SetTerrainCost((int)TravelType.GROUND_ONLY, (int)TerrainType.WATER, 10);
            map.SetTerrainCost((int)TravelType.AMPHIBIOUS, (int)TerrainType.GROUND, 2);

            LevelGeneration.InitialiseWaterPools(map);

            Level = new DemoLevel(map, Resolution);
            Level.SpawnAgentAt(new int2(0, (int)(LevelSize * 0.33f)), AgentType.MULTIPLE);
            Level.SpawnAgentAt(new int2(0, (int)(LevelSize * 0.66f)), AgentType.MULTIPLE);

        }

        void Update() {
            Level.Update();
            Level.VisualiseSectors(VisualiseConnections);
            Level.VisualiseAgentFlows();

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    SetAgentDestinations(mouseCell);
                }

            }

        }

        private void SetAgentDestinations(int2 newDestination) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = Level.GetEntityArray<AgentData>();
            foreach (var agent in agents) {
                em.SetComponentData(agent, new FlowGoal {
                    HasGoal = true,
                    Goal = newDestination
                });
            }
        }

    }

}