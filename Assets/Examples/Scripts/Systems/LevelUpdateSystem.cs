using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlowTiles.Examples {

    [BurstCompile]
    public partial struct LevelUpdateSystem : ISystem {

        public const bool VISUALISE_GRAPH_COLORS = false;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var setup = SystemAPI.GetSingleton<LevelSetup>();

            new WallsJob {
                LevelSize = setup.Size,
                LevelWalls = setup.Walls,
                LevelColors = setup.Colors,
            }.ScheduleParallel();

            new FlowJob {
                LevelSize = setup.Size,
                LevelFlows = setup.Flows,
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct WallsJob : IJobEntity {

            public int2 LevelSize;
            [ReadOnly] public NativeField<bool> LevelWalls;
            [ReadOnly] public NativeField<float4> LevelColors;

            [BurstCompile]
            private void Execute(Aspect wall, [ChunkIndexInQuery] int sortKey) {
                var cell = wall.Cell;
                var data = LevelWalls[cell.x, cell.y];

                var brightness = data ? 0f : 1;
                wall.Color = new float4(brightness);

                if (VISUALISE_GRAPH_COLORS) {
                    wall.Color *= LevelColors[cell.x, cell.y];
                }
            }

            public readonly partial struct Aspect : IAspect {

                public readonly Entity Entity;
                private readonly RefRW<WallData> _wall;
                private readonly RefRW<ColorOverride> _color;

                public int2 Cell => _wall.ValueRO.cell;

                public float4 Color {
                    get => _color.ValueRW.Value;
                    set => _color.ValueRW.Value = value;
                }

            }

        }

        [BurstCompile]
        public partial struct FlowJob : IJobEntity {

            public int2 LevelSize;
            [ReadOnly] public NativeField<float2> LevelFlows;

            [BurstCompile]
            private void Execute(Aspect flow, [ChunkIndexInQuery] int sortKey) {
                var cell = flow.Cell;
                var data = LevelFlows[cell.x, cell.y];

                flow.SetFlow(data);
            }

            public readonly partial struct Aspect : IAspect {

                public readonly Entity Entity;
                private readonly RefRW<FlowData> _flow;
                private readonly RefRW<LocalTransform> _transform;

                public int2 Cell => _flow.ValueRO.cell;

                public void SetFlow(float2 newFlow) {
                    if (math.length(newFlow) == 0) {
                        _transform.ValueRW.Scale = 0;
                    }
                    else {
                        var angle = math.atan2(newFlow.y, newFlow.x);
                        _transform.ValueRW.Scale = 1;
                        _transform.ValueRW.Rotation = quaternion.Euler(
                            new float3(0, 0, angle)
                        );
                    }
                }

            }

        }

    }

}