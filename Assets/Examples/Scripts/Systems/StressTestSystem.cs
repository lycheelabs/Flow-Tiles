using FlowTiles.ECS;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.Examples {

    public partial struct StressTestSystem : ISystem {

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        public void OnUpdate(ref SystemState state) {
            var level = SystemAPI.GetSingleton<LevelSetup>();
            new Job {
                LevelSize = level.Size
            }.Schedule();
        }

        [BurstCompile]
        public partial struct Job : IJobEntity {

            public int2 LevelSize;

            [BurstCompile]
            private void Execute(ref FlowPosition cell, ref FlowGoal goal, ref LocalTransform transform, ref StressTestData data) {
                var random = data.Random;
                
                var position = cell.Position;
                if (!goal.HasGoal || position.y == 0) {

                    if (position.y == 0) {
                        var newPosition = new int2(random.NextInt(LevelSize.x), LevelSize.y - 1);
                        cell.Position = newPosition;
                        transform.Position = new float3(newPosition.x, newPosition.y, -1);
                    }

                    var newGoal = new int2(random.NextInt(LevelSize.x), 0);
                    newGoal.x = math.min(newGoal.x / 4 * 4 + 2, LevelSize.x - 1);
                    goal.Goal = newGoal; 
                    goal.HasGoal = true;

                }

                data.Random = random;
            }

        }
    }

}