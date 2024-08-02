using FlowTiles.ECS;
using FlowTiles.FlowFields;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoLevel {

        private const float ColorFading = 0.4f;

        private static float4[] graphColorings = new float4[] {
            new float4(ColorFading, 1f, ColorFading, 1),
            new float4(ColorFading, ColorFading, 1f, 1),
            new float4(1f, 1f, ColorFading, 1),
            new float4(ColorFading, 1f, 1f, 1),
            new float4(1f, ColorFading, 1f, 1),
            new float4(1f, ColorFading, ColorFading, 1),
        };

        // -----------------------------------------

        public int2 LevelSize;
        public int Resolution;
        public bool VisualiseConnections;

        private PathableLevel Level;
        private NativeField<float4> ColorData;
        private NativeField<float2> FlowData;
        private PathableGraph Graph;

        private List<SpawnAgentCommand> AgentSpawns = new List<SpawnAgentCommand>();

        public DemoLevel (PathableLevel level, int resolution) {
            Level = level;
            LevelSize = level.Size;
            Resolution = resolution;

            /*Map.SetObstacle(5, 2);
            Map.SetObstacle(6, 2);
            Map.SetObstacle(7, 2);
            Map.SetObstacle(8, 2);
            Map.SetObstacle(9, 2);*/

            // Create the graph
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Graph = new PathableGraph(Level.Bounds.SizeCells, Resolution);
            PathableGraph.BurstBuild(ref Graph, ref Level);
            stopwatch.Stop();
            Debug.Log(string.Format("Portal graph created in: {0} ms", (int)stopwatch.Elapsed.TotalMilliseconds));

            // Allocate visualisation data
            ColorData = new NativeField<float4>(LevelSize, Allocator.Persistent, initialiseTo: 1);
            FlowData = new NativeField<float2>(LevelSize, Allocator.Persistent);

            for (int y = 0; y < LevelSize.x; y++) {
                for (int x = 0; x < LevelSize.y; x++) {
                    var sector = Graph.GetCostSector(x, y);
                    var color = sector.Colors[x % Resolution, y % Resolution];
                    if (color > 0) {
                        ColorData[x, y] = graphColorings[(color - 1) % graphColorings.Length];
                    }
                }
            }

            // Initialise the ECS simulation
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(singleton);
            em.AddComponent<GlobalPathfindingData>(singleton);
            em.SetComponentData(singleton, new LevelSetup {
                Size = LevelSize,
                Walls = Level.Obstacles,
                Colors = ColorData,
                Flows = FlowData,
            });
            em.SetComponentData(singleton, new GlobalPathfindingData {
                Graph = Graph,
            });

            // Position the camera
            var halfViewedSize = ((float2)LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize.y / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize.x, halfViewedSize.y, -20);

        }

        public void Update() {
            if (AgentSpawns.Count > 0) {
                if (TryGetSingleton(out PrefabLinks prefabs)) {
                    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                    foreach (var spawn in AgentSpawns) {

                        var agent = em.Instantiate(prefabs.Agent);
                        em.SetComponentData(agent, new LocalTransform {
                            Position = new float3(spawn.Cell, -1),
                            Scale = 1,
                        });
                        em.SetComponentData(agent, new FlowPosition {
                            Position = spawn.Cell,
                        });
                        if (spawn.Type == AgentType.SINGLE) {
                            em.AddComponent<FlowDebugData>(agent);
                        }
                        if (spawn.Type == AgentType.STRESS_TEST) {
                            var seed = (uint)UnityEngine.Random.Range(0, 99999);
                            em.AddComponent<StressTestData>(agent);
                            em.SetComponentData(agent, new StressTestData {
                                Random = new Unity.Mathematics.Random(seed)
                            });
                        }
                    }
                    AgentSpawns.Clear();
                }
            }   
        }

        public void SpawnAgentAt (int2 cell, AgentType type) {
            AgentSpawns.Add(new SpawnAgentCommand {
                Cell = cell,
                Type = type,
            });
        }

        public void FlipWallAt(int2 cell) {
            Level.Obstacles[cell.x, cell.y] = !Level.Obstacles[cell.x, cell.y];
        }

        public void VisualiseSectors(bool visualiseConnections) {
            Visualisation.DrawSectors(Graph.Portals, visualiseConnections);
        }

        public void VisualiseTestPath(int2 start, int2 dest, bool showFlow) {

            var pathfinder = new PortalPathfinder(Graph);
            var path = new UnsafeList<PortalPathNode>(32, Allocator.Temp);
            var success = pathfinder.TryFindPath(start, dest, ref path);

            if (success) {
                // Visualise the path
                FlowData.InitialiseTo(0);

                if (path.Length > 0) {
                    for (int i = 0; i < path.Length; i++) {
                        var node = path[i];
                        var sector = Graph.Costs.Sectors[node.Position.SectorIndex];
                        var flow = FlowFieldJob.ScheduleAndComplete(sector, node.GoalBounds, node.Direction);

                        Visualisation.DrawPortal(node.GoalBounds);
                        if (showFlow) {
                            CopyFlowVisualisationData(flow);
                        }
                        else {
                            Visualisation.DrawPortalLink(path[i].GoalBounds.CentrePoint, path[i + 1].GoalBounds.CentrePoint);
                        }

                    }
                }
            }
        }

        public void VisualiseAgentFlows() {
            FlowData.InitialiseTo(0);
            
            var datas = GetComponentArray<FlowDebugData>();
            foreach (var data in datas) {
                CopyFlowVisualisationData(data.CurrentFlowTile);
            }
        }

        private void CopyFlowVisualisationData(FlowField flowField) {
            if (!flowField.Directions.IsCreated) return;

            var sector = Graph.Costs.Sectors[flowField.SectorIndex];
            var bounds = sector.Bounds;
            for (int x = 0; x < bounds.SizeCells.x; x++) {
                for (int y = 0; y < bounds.SizeCells.y; y++) {
                    var mapCell = new int2(x + bounds.MinCell.x, y + bounds.MinCell.y);
                    var flow = flowField.GetFlow(x, y);
                    var cellColor = sector.Colors[x, y];
                    if (FlowData[mapCell.x, mapCell.y].Equals(new float2(0)) || cellColor == flowField.Color) {
                        FlowData[mapCell.x, mapCell.y] = flow;
                    }
                }
            }
        }

        public bool TryGetSingleton<T> (out T data) where T : unmanaged, IComponentData {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var query = em.CreateEntityQuery(new ComponentType[] { typeof(T) });
            if (query.TryGetSingletonEntity<T>(out Entity singleton)) {
                data = em.GetComponentData<T>(singleton);              
                return true;
            } else {
                data = default;
                return false;
            }
        }

        public NativeArray<Entity> GetEntityArray<T>() where T : unmanaged, IComponentData {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var query = em.CreateEntityQuery(new ComponentType[] { typeof(T) });
            return query.ToEntityArray(Allocator.Temp);
        }

        public NativeArray<T> GetComponentArray<T>() where T : unmanaged, IComponentData {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var query = em.CreateEntityQuery(new ComponentType[] { typeof(T) });
            return query.ToComponentDataArray<T>(Allocator.Temp);
        }

    }

}