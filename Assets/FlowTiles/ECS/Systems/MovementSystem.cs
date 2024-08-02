using FlowTiles.Examples;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.ECS {

    public partial struct MovementSystem : ISystem {

        public void OnUpdate(ref SystemState state) {
            new RequestPathsJob {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public float DeltaTime;

            [BurstCompile]
            private void Execute(ref AgentData agent, ref FlowPosition cell, ref FlowDirection direction, ref LocalTransform transform) {
                var speed = agent.speed;
                var dir = direction.Direction;
                speed = math.lerp(speed, (float2)dir, math.saturate(DeltaTime * 5));
                agent.speed = speed;

                var position = transform.Position;
                position += new float3(speed * DeltaTime * 3.3f, 0);
                transform.Position = position;

                var newX = (int)math.round(position.x);
                var newY = (int)math.round(position.y);
                cell.Position = new int2(newX, newY);               
            }

        }
    }

}