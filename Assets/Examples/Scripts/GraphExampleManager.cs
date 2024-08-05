using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class GraphExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 20;
        public bool VisualiseConnections;

        private DemoLevel Level;
        private int2 startCell;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution);
            LevelGeneration.InitialiseRandomObstacles(map, false);

            Level = new DemoLevel(map, Resolution);

        }

        void Update() {
            Level.Update();
            Level.VisualiseSectors(VisualiseConnections);

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    startCell = mouseCell;
                }

                Level.VisualiseTestPath(startCell, mouseCell, true);
            }

        }

    }

}