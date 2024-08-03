using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class StressTestExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;

        private DemoLevel Level;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize);
            //LevelGeneration.InitialiseRandomObstacles(map, true);
            LevelGeneration.InitialiseRandomWalls(map, LevelSize / 5);

            Level = new DemoLevel(map, Resolution);

            for (int x = 0; x < LevelSize; x++) {
                for (int y = 0; y < LevelSize; y++) {
                    Level.SpawnAgentAt(new int2(x, y), AgentType.STRESS_TEST);
                }
            }

        }

        void Update() {
            Level.Update();
            //Level.VisualiseSectors(false);
        }

    }

}