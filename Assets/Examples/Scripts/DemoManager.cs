using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

        public int LevelSize = 1000;
        public int Resolution = 10;
        public bool VisualiseConnections;

        private DemoLevel Level;

        void Start() {

            // Initialise the map
            Level = new DemoLevel(LevelSize, Resolution);
            Level.SpawnAgentAt(0, true);

            // Position the camera
            var halfViewedSize = (LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);

        }

        void Update() {

            Level.VisualiseSectors(VisualiseConnections);
            Level.VisualiseAgentFlows();

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    SetAgentDestinations(mouseCell);
                    //Level.FlipWallAt(mouseCell);
                }

                //Level.VisualiseTestPath(default, mouseCell, true);
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