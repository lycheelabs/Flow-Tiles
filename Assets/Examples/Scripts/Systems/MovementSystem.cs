using FlowTiles.ECS;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.Examples {

    public partial struct MovementSystem : ISystem {

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        public void OnUpdate(ref SystemState state) {
            var level = SystemAPI.GetSingleton<LevelSetup>();
            new Job {
                LevelSize = level.Size,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct Job : IJobEntity {

            public int2 LevelSize;
            public float DeltaTime;

            [BurstCompile]
            private void Execute(ref AgentData agent, ref FlowPosition cell, ref FlowDirection direction, ref LocalTransform transform) {
                var speed = agent.speed;
                var dir = direction.Direction;
                speed = math.lerp(speed, (float2)dir, math.saturate(DeltaTime * 5));
                agent.speed = speed;

                var position = transform.Position;
                position += new float3(speed * DeltaTime * 3.3f, 0);
                position.x = math.clamp(position.x, 0, LevelSize.x - 1);
                position.y = math.clamp(position.y, 0, LevelSize.y - 1);

                transform.Position = position;

                var newX = (int)math.round(position.x);
                var newY = (int)math.round(position.y);
                cell.Position = new int2(newX, newY);               
            }

        }
    }

}