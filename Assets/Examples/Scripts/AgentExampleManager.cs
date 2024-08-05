using FlowTiles.ECS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class AgentExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;
        public bool VisualiseConnections;

        private DemoLevel Level;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);
            LevelGeneration.InitialiseRandomObstacles(map, false);

            Level = new DemoLevel(map, Resolution);
            Level.SpawnAgentAt(0, AgentType.SINGLE);

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
                if (Input.GetMouseButtonDown(1)) {
                    Level.FlipWallAt(mouseCell);
                }

            }

        }

        private void SetAgentDestinations(int2 newDestination) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = Level.GetEntityArray<AgentData>();;
            foreach (var agent in agents) {
                em.SetComponentData(agent, new FlowGoal {
                    HasGoal = true,
                    Goal = newDestination
                });
            }
        }

    }

}