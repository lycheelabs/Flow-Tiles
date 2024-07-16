using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

        public int LevelSize = 1000;
        public bool[,] WallMap;

        void Start() {
            var halfViewedSize = (LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);

            WallMap = new bool[LevelSize, LevelSize];
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    if (Random.value < 0.2f) WallMap[i, j] = true;
                }
            }

            var wallData = new NativeArray<bool>(LevelSize * LevelSize, Allocator.Persistent);
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    var index = i + j * LevelSize;
                    wallData[index] = WallMap[i, j];
                }
            }

            var levelSetup = new LevelSetup {
                Size = LevelSize,
                Walls = wallData,
            };

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(singleton);
            em.SetComponentData(singleton, levelSetup);

        }

        void Update() {
            //
        }

    }

}