using LycheeLabs.FlowTiles.ECS;
using LycheeLabs.FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LycheeLabs.FlowTiles.Examples {

    [BurstCompile]
    public partial struct LevelUpdateSystem : ISystem {

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<LevelSetup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var setup = SystemAPI.GetSingleton<LevelSetup>();

            new WallsJob {
                LevelSize = setup.Size,
                LevelWalls = setup.Walls,
                LevelTerrain = setup.Terrain,
                LevelStamps = setup.Obstacles,
                LevelColors = setup.Colors,
                VisualiseColors = setup.VisualiseColors,
            }.ScheduleParallel();

            new FlowJob {
                LevelSize = setup.Size,
                LevelFlows = setup.Flows,
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct WallsJob : IJobEntity {

            public int2 LevelSize;
            public bool VisualiseColors;
            [ReadOnly] public NativeField<bool> LevelWalls;
            [ReadOnly] public NativeField<byte> LevelTerrain;
            [ReadOnly] public NativeField<byte> LevelStamps;
            [ReadOnly] public NativeField<float4> LevelColors;

            [BurstCompile]
            private void Execute(
                [ChunkIndexInQuery] int sortKey,
                [ReadOnly] ref WallData wallData,
                ref ColorOverride colorOverride
            ) {
                var cell = wallData.cell;
                var wall = LevelWalls[cell.x, cell.y] || LevelStamps[cell.x, cell.y] >= 255;
                var terrain = LevelTerrain[cell.x, cell.y];

                float4 color = 1;
                if (terrain == (byte)TerrainType.WATER) {
                    color = new float4(0.2f, 0.36f, 1f, 1f);
                }
                if (VisualiseColors) {
                    color = LevelColors[cell.x, cell.y];
                    if (wall) {
                        color *= 0.16f;
                    }
                }
                else if (wall) {
                    color = 0;
                }

                colorOverride.Value = color;
            }

        }

        [BurstCompile]
        public partial struct FlowJob : IJobEntity {

            public int2 LevelSize;
            [ReadOnly] public NativeField<float2> LevelFlows;

            [BurstCompile]
            private void Execute(
                [ChunkIndexInQuery] int sortKey,
                [ReadOnly] ref FlowData flowData,
                ref LocalTransform transform
            ) {
                var cell = flowData.cell;
                var data = LevelFlows[cell.x, cell.y];

                if (math.length(data) == 0) {
                    transform.Scale = 0;
                }
                else {
                    var angle = math.atan2(data.y, data.x);
                    transform.Scale = 1;
                    transform.Rotation = quaternion.Euler(new float3(0, 0, angle));
                }
            }

        }

    }

}