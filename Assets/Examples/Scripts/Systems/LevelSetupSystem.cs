using FlowTiles.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.Examples {

    public partial struct LevelSetupSystem : ISystem {

        private static bool instantiated = false;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<PrefabLinks>();
            state.RequireForUpdate<LevelSetup>();
        }

        public void OnUpdate (ref SystemState state) {
            if (instantiated) {
                return;
            }
            instantiated = true;
            
            var setup = SystemAPI.GetSingleton<LevelSetup>();
            var prefabs = SystemAPI.GetSingleton<PrefabLinks>();

            for (int i = 0; i < setup.Size.x; i++) {
                for (int j = 0; j < setup.Size.y; j++) {

                    // Create wall
                    var wall = state.EntityManager.Instantiate(prefabs.Wall);
                    state.EntityManager.SetComponentData(wall, new WallData {
                        cell = new int2(i, j),
                    });
                    state.EntityManager.SetComponentData(wall, new LocalTransform {
                        Position = new float3(i, j, 0f),
                        Scale = 1,
                    });

                    // Create flow marker
                    var flow = state.EntityManager.Instantiate(prefabs.Flow);
                    state.EntityManager.SetComponentData(flow, new FlowData {
                        cell = new int2(i, j),
                    });
                    state.EntityManager.SetComponentData(flow, new LocalTransform {
                        Position = new float3(i, j, -0.5f),
                        Scale = 0,
                    });

                }
            }

        }

    }

}