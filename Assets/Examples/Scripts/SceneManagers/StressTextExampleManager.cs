using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class StressTestExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;

        private DemoLevel Level;

        void Awake() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);
            LevelGeneration.InitialiseRandomWalls(map, LevelSize / 5);

            Level = new DemoLevel(map, Resolution);

            for (int x = 0; x < LevelSize; x++) {
                for (int y = 0; y < LevelSize; y++) {
                    if (!map.Obstacles[x, y]) {
                        Level.SpawnAgentAt(new int2(x, y), AgentType.STRESS_TEST);
                    }
                }
            }

        }

        void Update() {
            Level.Update(); 
            
            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(1)) {
                    Level.FlipWallAt(mouseCell);
                }
            }

        }

        void OnDestroy() {
            Level.Dispose();
        }

    }

}