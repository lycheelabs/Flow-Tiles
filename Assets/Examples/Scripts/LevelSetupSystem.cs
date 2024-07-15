using Unity.Burst;
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
            if (!instantiated) {
                instantiated = true;
            }

            var setup = SystemAPI.GetSingleton<LevelSetup>();
            var prefabs = SystemAPI.GetSingleton<PrefabLinks>();
            var wall = prefabs.Wall;

            for (int i = 0; i < setup.Size; i++) {
                for (int j = 0; j < setup.Size; j++) {
                    var entity = state.EntityManager.Instantiate(wall);
                    state.EntityManager.SetComponentData(entity, new LocalTransform {
                        Position = new float3(i, j, 0),
                        Scale = 1,
                    });
                }
            }
        }

    }

}