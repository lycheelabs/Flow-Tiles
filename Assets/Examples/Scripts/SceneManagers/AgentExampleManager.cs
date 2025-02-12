using FlowTiles.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class AgentExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;
        public bool AddRandomWalls;
        public PathSmoothingMode PathSmoothingMode;
        public VisualiseMode VisualiseMode;

        private DemoLevel Level;

        void Awake() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);
            if (AddRandomWalls) {
                LevelGeneration.InitialiseRandomObstacles(map, false);
            }

            Level = new DemoLevel(map, Resolution);
            Level.SpawnAgentAt(0, AgentType.SINGLE, PathSmoothingMode);

        }

        void Update() {
            Level.Update();
            Level.VisualiseMode = VisualiseMode;
            Level.VisualiseAgentFlows();

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    var isBlocked = Level.GetWallAt(mouseCell);
                    if (!isBlocked) {
                        Level.SetAgentDestinations(mouseCell, PathSmoothingMode);
                    }
                }
                if (Input.GetMouseButtonDown(1)) {
                    Level.FlipWallAt(mouseCell);
                }

            }
        }

        void OnDestroy() {
            Level?.Dispose();
        }

    }

}