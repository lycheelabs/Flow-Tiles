using FlowTiles.ECS;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.Examples {

    [BurstCompile]
    public partial struct StressTestSystem : ISystem {

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var level = SystemAPI.GetSingleton<LevelSetup>();
            new Job {
                LevelSize = level.Size
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct Job : IJobEntity {

            public int2 LevelSize;

            [BurstCompile]
            private void Execute(ref AgentData agent, ref FlowPosition cell, ref FlowProgress progress, ref FlowGoal goal, ref LocalTransform transform, ref StressTestData data) {
                var random = data.Random;
                
                var position = cell.PositionCell;
                if (!goal.HasGoal || position.y == 0) {

                    if (position.y <= 0) {
                        var newPosition = new int2(random.NextInt(LevelSize.x), LevelSize.y - 1);
                        cell.Position = newPosition;
                        transform.Position = new float3(newPosition.x, newPosition.y, transform.Position.z);
                        agent.Speed = 0;
                    }
                    
                    var newGoal = new int2(random.NextInt(LevelSize.x), 0);
                    newGoal.x = math.min(newGoal.x / 4 * 4 + 2, LevelSize.x - 1);
                    goal.Goal = newGoal; 
                    goal.HasGoal = true;
                    progress.HasPath = false;
                }

                data.Random = random;
            }

        }
    }

}