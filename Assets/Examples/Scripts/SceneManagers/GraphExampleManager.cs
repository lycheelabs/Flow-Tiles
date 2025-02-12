using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class GraphExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 20;
        public bool AddRandomWalls;
        public VisualiseMode VisualiseMode;
        public bool VisualiseLineOfSightSmoothing;

        private DemoLevel Level;
        private int2 startCell;

        void Awake() {
            var map = new PathableLevel(LevelSize, LevelSize, Resolution, 1, 2);
            if (AddRandomWalls) {
                LevelGeneration.InitialiseRandomObstacles(map, false);
            }

            Level = new DemoLevel(map, Resolution);
        }

        void Update() {
            Level.Update();
            Level.VisualiseMode = VisualiseMode;

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    startCell = mouseCell;
                }
                if (Input.GetMouseButtonDown(1)) {
                    Level.FlipWallAt(mouseCell);
                }

                Level.VisualiseTestPath(startCell, mouseCell, true, VisualiseLineOfSightSmoothing);
            }

        }

        void OnDestroy() {
            Level?.Dispose();
        }

    }

}