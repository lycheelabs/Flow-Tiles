using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class ChangingStressTestExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;
        public float Frequency = 1f;

        private DemoLevel Level;

        void Awake() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);

            Level = new DemoLevel(map, Resolution);

            var direction = 1;
            for (int i = Resolution / 2; i < LevelSize; i += Resolution) {
                Level.AddMovingWall(i, LevelSize / 2, direction, Frequency);
                direction *= -1;
            }

            for (int x = 0; x < LevelSize; x++) {
                for (int y = 0; y < LevelSize; y++) {
                    if (map.Obstacles[x, y] == 0) {
                        Level.SpawnAgentAt(new int2(x, y), AgentType.STRESS_TEST);
                    }
                }
            }

        }

        void Update() {
            Level.Update(); 
        }

        void OnDestroy() {
            Level?.Dispose();
        }

    }

}