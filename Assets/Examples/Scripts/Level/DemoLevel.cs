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

        private static float4[] ColorList = new float4[] {
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
        private List<MovingWall> MovingWalls = new List<MovingWall>();

        public DemoLevel (PathableLevel level, int resolution) {
            Level = level;
            LevelSize = level.Size;
            Resolution = resolution;

            // Create the graph
            Graph = new PathableGraph(LevelSize.x, LevelSize.y, Resolution, level.NumTravelTypes);

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
                Walls = Level.Blocked,
                Terrain = Level.Terrain,
                Obstacles = Level.Obstacles,
                Flows = FlowData,
                VisualiseColors = false,
                Colors = ColorData,
            });
            em.SetComponentData(Singleton, new GlobalPathfindingData {
                IsInitialised = true,
                Level = level,
                Graph = Graph,
            });

            // Position the camera
            var halfViewedSize = ((float2)LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize.y / 2f * 1.06f;
            Camera.main.transform.position = new Vector3(halfViewedSize.x, halfViewedSize.y, -20);

        }

        public void Dispose () {
            Level.Dispose();
            ColorData.Dispose();
            FlowData.Dispose();
            Graph.Dispose();

            foreach (var wall in MovingWalls) {
                wall.Dispose();
            }
        }

        public void Update() {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var showColors = VisualiseMode == VisualiseMode.Costs 
                || VisualiseMode == VisualiseMode.Islands
                || VisualiseMode == VisualiseMode.Continents;

            em.SetComponentData(Singleton, new LevelSetup {
                Size = LevelSize,
                Walls = Level.Blocked,
                Terrain = Level.Terrain,
                Obstacles = Level.Obstacles,
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
                            Position = new float3(spawn.Cell, -1 - UnityEngine.Random.Range(0, 4)),
                            Scale = 1,
                        });
                        em.SetComponentData(agent, new FlowPosition {
                            Position = spawn.Cell,
                        });
                        em.SetComponentData(agent, new FlowGoal {
                            SmoothingMode = spawn.LOSMode,
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

            foreach (var wall in MovingWalls) {
                wall.Update();
            }

            Visualisation.DrawSectors(Graph);
            if (VisualiseMode == VisualiseMode.Portals) {
                Visualisation.DrawSectorPortals(Graph, VisualisedTravelType);
            }
            if (VisualiseMode == VisualiseMode.Connections) {
                Visualisation.DrawSectorConnections(Graph, VisualisedTravelType);
            }

            if (VisualiseMode == VisualiseMode.Costs) {
                for (int y = 0; y < LevelSize.x; y++) {
                    for (int x = 0; x < LevelSize.y; x++) {
                        var sector = Graph.CellToSectorMap(new int2(x, y), VisualisedTravelType);
                        var cost = sector.Costs.Cells[x % Resolution, y % Resolution] - 1;
                        var alpha = math.pow(math.clamp(cost / 100f, 0, 1), 0.25f);
                        var fade = 1 - alpha * 0.8f;
                        var color = new float4(1, fade, fade, 1);
                        ColorData[x, y] = color;
                    }
                }
            }
            if (VisualiseMode == VisualiseMode.Islands) {
                for (int y = 0; y < LevelSize.x; y++) {
                    for (int x = 0; x < LevelSize.y; x++) {
                        var sector = Graph.CellToSectorMap(new int2(x, y), 0);
                        var island = sector.Islands.Cells[x % Resolution, y % Resolution];
                        if (island > 0) {
                            ColorData[x, y] = ColorList[(island - 1) % ColorList.Length];
                        }
                        else {
                            ColorData[x, y] = 1;
                        }
                    }
                }
            }
            if (VisualiseMode == VisualiseMode.Continents) {
                for (int y = 0; y < LevelSize.x; y++) {
                    for (int x = 0; x < LevelSize.y; x++) {
                        var sector = Graph.CellToSectorMap(new int2(x, y), 0);
                        var island = sector.Islands.Cells[x % Resolution, y % Resolution];

                        ColorData[x, y] = 1;
                        if (island > 0) {
                            var continent = sector.Portals.Roots[island - 1].Continent;
                            if (continent > 0) {
                                ColorData[x, y] = ColorList[(continent - 1) % ColorList.Length];
                            }
                        }
                    }
                }
            }
        }

        public void SpawnAgentAt (int2 cell, AgentType type, PathSmoothingMode losMode, int travelType = 0) {
            AgentSpawns.Add(new SpawnAgentCommand {
                Cell = cell,
                Type = type,
                TravelType = travelType,
                LOSMode = losMode,
            });
        }

        public void AddMovingWall(int2 corner, int length, int direction, float frequency) {
            MovingWalls.Add(new MovingWall(corner, length, direction, MovingWalls.Count, Level, frequency));
        }

        public bool GetWallAt (int2 cell) {
            return Level.Blocked[cell.x, cell.y];
        }

        public void FlipWallAt(int2 cell) {
            var wall = Level.Blocked[cell.x, cell.y];
            Level.SetBlocked(cell.x, cell.y, !wall);
        }

        public void FlipTerrainAt(int2 cell) {
            var terrain = Level.Terrain[cell.x, cell.y];
            var newTerrain = (terrain == 0 ? 1 : 0);
            Level.SetTerrain(cell.x, cell.y, (byte)newTerrain);
        }

        public void SetTerrainAt(int2 cell, TerrainType type) {
            Level.SetTerrain(cell.x, cell.y, (byte)type);
        }

        public void VisualiseTestPath(int2 start, int2 dest, bool showFlow, bool showLosSmoothing = false) {
            FlowData.InitialiseTo(0);

            if (!Graph.SectorIsInitialised(Graph.CellToIndex(start))) return;
            if (!Graph.SectorIsInitialised(Graph.CellToIndex(dest))) return;

            var travelType = 0;
            var startFlow = CalculateFlow(Graph.CellToSectorMap(start, 0), new CellRect(start), 0);
            var destFlow = CalculateFlow(Graph.CellToSectorMap(dest, 0), new CellRect(dest), 0);
            var pathfinder = new PortalPathfinder(Graph, Constants.EXPECTED_MAX_SEARCHED_NODES, Allocator.Temp);
            var path = new UnsafeList<PortalPathNode>(Constants.EXPECTED_MAX_PATH_LENGTH, Allocator.Temp);

            var success = pathfinder.TryFindPath(start, startFlow, dest, destFlow, travelType, ref path);

            if (success) {
                // Visualise the path
                FlowData.InitialiseTo(0);

                // Draw portals
                Visualisation.DrawRect(new CellRect(start), Color.green);
                for (int i = 0; i < path.Length; i++) {
                    Visualisation.DrawRect(path[i].GoalBounds, Color.green);
                }

                // Draw links
                if (path.Length > 0) {
                    Visualisation.DrawPortalLink(start, path[0].GoalBounds.CentrePoint, Color.green);
                    for (int i = 0; i < path.Length - 1; i++) {
                        var p1 = path[i].GoalBounds.CentrePoint;
                        var p2 = path[i + 1].GoalBounds.CentrePoint;
                        Visualisation.DrawPortalLink(p1, p2, Color.green);
                    }

                    if (showLosSmoothing) {
                        // Show line of sight smoothing
                        var p1 = start;
                        for (int i = 0; i < path.Length; i++) {
                            var p2 = FlowTileUtils.GetBestPathLineOfSight(p1, i, path, ref Graph, 0);
                            if (!p2.Equals(p1)) {

                                var color = Color.blue;
                                Visualisation.DrawPortalLink(p1, p2, color);
                                Visualisation.DrawRect(new CellRect(p1), color);
                                Visualisation.DrawRect(new CellRect(p2), color);

                                // Skip over smoothed nodes
                                while (i < path.Length - 1 && !path[i].GoalBounds.ContainsCell(p2)) {
                                    i++;
                                }
                            } 
                            else {
                                p2 = path[i].GoalBounds.CentreCell;
                            }
                            p1 = p2;
                        }
                    }
                }

                // Draw flow
                if (showFlow) {
                    for (int i = 0; i < path.Length; i++) {
                        var node = path[i];
                        var sector = Graph.IndexToSectorMap(node.Position.SectorIndex, travelType);
                        var flow = CalculateFlow(sector, node.GoalBounds, node.Direction);
                        CopyFlowVisualisationData(flow, travelType);
                        flow.Dispose();
                    }
                }
            }

            startFlow.Dispose();
            destFlow.Dispose();
            path.Dispose();
        }

        private FlowField CalculateFlow (SectorData sector, CellRect goalBounds, int2 direction) {
            var map = Graph.IndexToSectorMap(sector.Index, 0);
            var flow = new UnsafeField<float2>(map.Bounds.SizeCells, Allocator.Temp);
            var dist = new UnsafeField<int>(map.Bounds.SizeCells, Allocator.Temp);
            var task = new FindFlowsJob.Task {
                CacheKey = 0,
                Sector = map,
                GoalBounds = goalBounds,
                ExitDirection = direction,
                Flow = flow,
                Distances = dist,
            };
            var calculator = new FlowCalculator(task, Allocator.Temp);
            calculator.Calculate(ref flow, ref dist);
            return task.ResultAsFlowField();
        }

        public void VisualiseAgentFlows(int travelType = 0) {
            FlowData.InitialiseTo(0);
            
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = GetEntityArray<FlowDebugData>();
            foreach (var agent in agents) {
                var myTravelType = em.GetComponentData<AgentData>(agent).TravelType;
                if (myTravelType == travelType) {
                    var myFlowData = em.GetComponentData<FlowDebugData>(agent).CurrentFlowTile;
                    CopyFlowVisualisationData(myFlowData, travelType);
                }
            }
        }

        private void CopyFlowVisualisationData(FlowField flowField, int travelType) {
            if (!flowField.Directions.IsCreated) return;

            var bounds = Graph.Layout.GetSectorBounds(flowField.SectorIndex);
            for (int x = 0; x < bounds.SizeCells.x; x++) {
                for (int y = 0; y < bounds.SizeCells.y; y++) {
                    var islands = Graph.IndexToSectorMap(flowField.SectorIndex, travelType).Islands;
                    var mapCell = new int2(x + bounds.MinCell.x, y + bounds.MinCell.y);
                    var flow = flowField.GetFlow(x, y);
                    var island = islands.Cells[x, y];
                    if (island == flowField.IslandIndex) {
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

        public void SetAgentDestinations(int2 newDestination, PathSmoothingMode smoothingMode) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var agents = GetEntityArray<AgentData>();
            foreach (var agent in agents) {
                var data = em.GetComponentData<AgentData>(agent);
                em.SetComponentData(agent, new FlowGoal {
                    HasGoal = true,
                    Goal = newDestination,
                    TravelType = data.TravelType,
                    SmoothingMode = smoothingMode,
                });
            }
        }

    }

}