using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.Examples {

    public partial struct LevelUpdateSystem : ISystem {

        public const bool VISUALISE_GRAPH_COLORS = false;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        public void OnUpdate(ref SystemState state) {
            var setup = SystemAPI.GetSingleton<LevelSetup>();
            new Job {
                LevelSize = setup.Size,
                LevelWalls = setup.Walls,
                LevelColors = setup.Colors,
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct Job : IJobEntity {

            public int2 LevelSize;
            [ReadOnly] public NativeArray<bool> LevelWalls;
            [ReadOnly] public NativeArray<float4> LevelColors;

            [BurstCompile]
            private void Execute(Aspect wall, [ChunkIndexInQuery] int sortKey) {
                var cell = wall.Cell;
                var index = cell.x + cell.y * LevelSize.x;
                var data = LevelWalls[index];

                var brightness = data ? 0f : 1;
                wall.Color = new float4(brightness);

                if (VISUALISE_GRAPH_COLORS) {
                    wall.Color *= LevelColors[index];
                }
            }
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