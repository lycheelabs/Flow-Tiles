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

    public enum VisualiseMode {
        NONE,
        CONNECTIONS, 
        COLORS, 
        ISLANDS
    }
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
        public VisualiseMode VisualiseMode;
        public int VisualisedTravelType;

        private PathableLevel Level;
        private NativeField<float4> ColorData;
        private NativeField<float2> FlowData;
        private PathableGraph Graph;
        private Entity Singleton;

        private List<SpawnAgentCommand> AgentSpawns = new List<SpawnAgentCommand>();

        public DemoLevel (PathableLevel level, int resolution) {
            Level = level;
            LevelSize = level.Size;
            Resolution = resolution;

            // Create the graph
            Graph = new PathableGraph(Level.Bounds.SizeCells, Resolution, level.NumTravelTypes);

            // Allocate visualisation data
            ColorData = new NativeField<float4>(LevelSize, Allocator.Persistent, initialiseTo: 1);
            FlowData = new NativeField<float2>(LevelSize, Allocator.Persistent);

            // Initialise the ECS simulation
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(Singleton);
            em.AddComponent<GlobalPathfindingData>(Singleton);
            em.SetComponentData(Singleton, new LevelSetup {
                Size = LevelSize,
                Walls = Level.Obstacles,
                Terrain = Level.Terrain,
                Flows = FlowData,
                VisualiseColors = false,
                Colors = ColorData,
            });
            em.SetComponentData(Singleton, new GlobalPathfindingData {
                Level = level,
                Graph = Graph,
            });

            // Position the camera
            var halfViewedSize = ((float2)LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize.y / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize.x, halfViewedSize.y, -20);

        }

        public void Update() {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var showColors = VisualiseMode == VisualiseMode.COLORS || VisualiseMode == VisualiseMode.ISLANDS;
            em.SetComponentData(Singleton, new LevelSetup {
                Size = LevelSize,
                Walls = Level.Obstacles,
                Terrain = Level.Terrain,
                Flows = FlowData,
                VisualiseColors = showColors,
                Colors = ColorData,
            });

            if (AgentSpawns.Count > 0) {
                if (TryGetSingleton(out PrefabLinks prefabs)) {
                    foreach (var spawn in AgentSpawns) {

                        var agent = em.Instantiate(prefabs.Agent);
                        em.SetComponentData(agent, new AgentData {
                            TravelType = spawn.TravelType,
                        });
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
                            var seed = (uint)UnityEngine.Random.Range(1, 99999);
                            em.AddComponent<StressTestData>(agent);
                            em.SetComponentData(agent, new StressTestData {
                                Random = new Unity.Mathematics.Random(seed)
                            });
                        }
                    }
                    AgentSpawns.Clear();
                }
            }

            Visualisation.DrawSectors(Graph);
            if (VisualiseMode == VisualiseMode.CONNECTIONS) {
                Visualisation.DrawSectorConnections(Graph, VisualisedTravelType);
            }

            if (VisualiseMode == VisualiseMode.COLORS) {
                for (int y = 0; y < LevelSize.x; y++) {
                    for (int x = 0; x < LevelSize.y; x++) {
                        var sector = Graph.CellToSectorMap(new int2(x, y), 0);
                        var color = sector.Colors.Cells[x % Resolution, y % Resolution];
                        if (color > 0) {
                            ColorData[x, y] = graphColorings[(color - 1) % graphColorings.Length];
                        } else {
                            ColorData[x, y] = 1;
                        }
                    }
                }
            }
            if (VisualiseMode == VisualiseMode.ISLANDS) {
                for (int y = 0; y < LevelSize.x; y++) {
                    for (int x = 0; x < LevelSize.y; x++) {
                        var sector = Graph.CellToSectorMap(new int2(x, y), 0);
                        var color = sector.Islands.Cells[x % Resolution, y % Resolution];
                        if (color > 0) {
                            ColorData[x, y] = graphColorings[(color - 1) % graphColorings.Length];
                        }
                        else {
                            ColorData[x, y] = 1;
                        }
                    }
                }
            }
        }

        public void SpawnAgentAt (int2 cell, AgentType type, int travelType = 0) {
            AgentSpawns.Add(new SpawnAgentCommand {
                Cell = cell,
                Type = type,
                TravelType = travelType,
            });
        }

        public void FlipWallAt(int2 cell) {
            var wall = Level.Obstacles[cell.x, cell.y];
            Level.SetObstacle(cell.x, cell.y, !wall);
        }

        public void FlipTerrainAt(int2 cell) {
            var terrain = Level.Terrain[cell.x, cell.y];
            var newTerrain = (terrain == 0 ? 1 : 0);
            Level.SetTerrain(cell.x, cell.y, (byte)newTerrain);
        }

        public void SetTerrainAt(int2 cell, TerrainType type) {
            Level.SetTerrain(cell.x, cell.y, (byte)type);
        }

        public void VisualiseTestPath(int2 start, int2 dest, bool showFlow) {

            var pathfinder = new PortalPathfinder(Graph);
            var path = new UnsafeList<PortalPathNode>(32, Allocator.Temp);
            var success = pathfinder.TryFindPath(start, dest, 0, ref path);

            if (success) {
                // Visualise the path
                FlowData.InitialiseTo(0);

                // Draw portals
                for (int i = 0; i < path.Length; i++) {
                    Visualisation.DrawRect(path[i].GoalBounds, Color.green);
                }

                // Draw links
                if (path.Length > 0) {
                    Visualisation.DrawPortalLink(start, path[0].GoalBounds.CentrePoint);
                    for (int i = 0; i < path.Length - 1; i++) {
                        Visualisation.DrawPortalLink(path[i].GoalBounds.CentrePoint, path[i + 1].GoalBounds.CentrePoint);
                    }
                }

                // Draw flow
                if (showFlow) {
                    for (int i = 0; i < path.Length; i++) {
                        var node = path[i];
                        var map = Graph.IndexToSectorMap(node.Position.SectorIndex, 0);
                        var flow = CalculateFlowJob.ScheduleAndComplete(map, node.GoalBounds, node.Direction);
                        CopyFlowVisualisationData(flow);
                    }
                }
            }
        }

        public void VisualiseAgentFlows(int travelType = 0) {
            FlowData.InitialiseTo(0);
            
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = GetEntityArray<FlowDebugData>();
            foreach (var agent in agents) {
                var myTravelType = em.GetComponentData<AgentData>(agent).TravelType;
                if (myTravelType == travelType) {
                    var myFlowData = em.GetComponentData<FlowDebugData>(agent).CurrentFlowTile;
                    CopyFlowVisualisationData(myFlowData);
                }
            }
        }

        private void CopyFlowVisualisationData(FlowField flowField) {
            if (!flowField.Directions.IsCreated) return;

            var bounds = Graph.Layout.GetSectorBounds(flowField.SectorIndex);
            for (int x = 0; x < bounds.SizeCells.x; x++) {
                for (int y = 0; y < bounds.SizeCells.y; y++) {
                    var colors = Graph.CellToSectorMap(new int2(x, y), 0).Colors;
                    var mapCell = new int2(x + bounds.MinCell.x, y + bounds.MinCell.y);
                    var flow = flowField.GetFlow(x, y);
                    var cellColor = colors.Cells[x, y];
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

        public void SetAgentDestinations(int2 newDestination) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = GetEntityArray<AgentData>();
            foreach (var agent in agents) {
                var data = em.GetComponentData<AgentData>(agent);
                em.SetComponentData(agent, new FlowGoal {
                    HasGoal = true,
                    Goal = newDestination,
                    TravelType = data.TravelType,
                });
            }
        }

    }

}