using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class ChangingStressTestExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;

        private DemoLevel Level;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);

            Level = new DemoLevel(map, Resolution);

            var direction = 1;
            for (int i = Resolution / 2; i < LevelSize; i += Resolution) {
                Level.AddMovingWall(i, LevelSize / 2, direction);
                direction *= -1;
            }

            for (int x = 0; x < LevelSize; x++) {
                for (int y = 0; y < LevelSize; y++) {
                    if (map.Stamps[x, y] == 0) {
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

    }

}