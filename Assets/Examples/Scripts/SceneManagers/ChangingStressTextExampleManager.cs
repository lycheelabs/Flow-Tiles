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
                AddMovingWalls(i, direction);
                direction *= -1;
            }

            for (int x = 0; x < LevelSize; x++) {
                for (int y = 0; y < LevelSize; y++) {
                    if (!map.Obstacles[x, y]) {
                        Level.SpawnAgentAt(new int2(x, y), AgentType.STRESS_TEST);
                    }
                }
            }

        }

        private void AddMovingWalls (int2 corner, int direction) {
            var wallLength = LevelSize / 3;
            var mid = Level.LevelSize.x / 2;
            Level.AddMovingWall(corner, wallLength, direction);
            /*if (corner.x < mid) {
                Level.AddMovingWall(corner + new int2(mid, 0), wallLength, direction);
            } else {
                Level.AddMovingWall(corner - new int2(mid, 0), wallLength, direction);
            }*/
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