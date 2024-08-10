using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class GraphExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 20;
        public bool VisualiseConnections;
        public bool VisualiseColors;

        private DemoLevel Level;
        private int2 startCell;

        void Start() {

            var map = new PathableLevel(LevelSize, LevelSize, Resolution, 1, 2);
            map.SetTerrainCost(0, (int)TerrainType.WATER, 3);
            LevelGeneration.InitialiseRandomObstacles(map, false);

            Level = new DemoLevel(map, Resolution);

        }

        void Update() {
            Level.Update();
            Level.VisualiseSectors(VisualiseConnections);
            Level.VisualiseColors = VisualiseColors;

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    startCell = mouseCell;
                }
                if (Input.GetMouseButtonDown(1)) {
                    //Level.FlipWallAt(mouseCell);
                    Level.SetTerrainAt(mouseCell, TerrainType.WATER);
                }

                //Level.VisualiseTestPath(startCell, mouseCell, true);
            }

        }

    }

}