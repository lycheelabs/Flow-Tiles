using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class TerrainExampleManager : MonoBehaviour {

        public int LevelSize = 100;
        public int Resolution = 10;
        public TravelType VisualisedTravelType;
        public VisualiseMode VisualiseMode;

        private DemoLevel Level;

        void Awake() {

            // Initialise the map with terrain and travel types
            var map = new PathableLevel(LevelSize, LevelSize, Resolution, 2, 2);
            map.SetTerrainCost((int)TravelType.GROUND_ONLY, (int)TerrainType.WATER, 10);
            map.SetTerrainCost((int)TravelType.AMPHIBIOUS, (int)TerrainType.GROUND, 2);

            LevelGeneration.InitialiseWaterPools(map);

            Level = new DemoLevel(map, Resolution);

            var pos1 = new int2(0, (int)(LevelSize * 0.33f));
            var pos2 = new int2(0, (int)(LevelSize * 0.66f));
            Level.SpawnAgentAt(pos1, AgentType.SINGLE, travelType: (int)TravelType.GROUND_ONLY);
            Level.SpawnAgentAt(pos2, AgentType.SINGLE, travelType: (int)TravelType.AMPHIBIOUS);

        }

        void Update() {
            Level.Update();
            Level.VisualiseMode = VisualiseMode;
            Level.VisualisedTravelType = (int)VisualisedTravelType;

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                if (Input.GetMouseButtonDown(0)) {
                    Level.SetAgentDestinations(mouseCell);
                }

            }

        }

        void OnDestroy() {
            Level?.Dispose();
        }

    }

}