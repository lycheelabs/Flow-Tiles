using FlowTiles.FlowField;
using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using Unity.Collections;
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

        public int LevelSize = 1000;
        public int Resolution = 10;
        public bool VisualiseConnections;

        private PathableLevel Map;
        private NativeField<float4> ColorData;
        private NativeField<float2> FlowData;
        private PathableGraph Graph;

        public DemoLevel (int levelSize, int resolution) {
            LevelSize = levelSize;
            Resolution = resolution;

            // Initialise the map
            Map = new PathableLevel(LevelSize, LevelSize);
            Map.InitialiseRandomObstacles();

            // Create the graph
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Graph = new PathableGraph(Map.Bounds.SizeCells, Resolution);
            PathableGraph.BurstBuild(ref Graph, ref Map);
            stopwatch.Stop();
            Debug.Log(string.Format("Portal graph created in: {0} ms", (int)stopwatch.Elapsed.TotalMilliseconds));

            // Allocate visualisation data
            ColorData = new NativeField<float4>(LevelSize, Allocator.Persistent, initialiseTo: 1);
            FlowData = new NativeField<float2>(LevelSize, Allocator.Persistent);

            for (int y = 0; y < LevelSize; y++) {
                for (int x = 0; x < LevelSize; x++) {
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
                Walls = Map.Obstacles,
                Colors = ColorData,
                Flows = FlowData,
            });
            em.SetComponentData(singleton, new GlobalPathfindingData {
                Graph = Graph,
            });

        }

        public void SpawnAgentAt (int2 cell, bool addDebugData = false) {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (TryGetSingleton(out PrefabLinks prefabs)) {
                var agent = em.Instantiate(prefabs.Agent);
                em.SetComponentData(agent, new LocalTransform {
                    Position = new float3(cell, -1),
                    Scale = 1,
                });
                em.SetComponentData(agent, new FlowPosition {
                    Position = cell,
                });
                if (addDebugData) {
                    em.AddComponent<FlowDebugData>(agent);
                }
            }
        }

        public void FlipWallAt(int2 cell) {
            Map.Obstacles[cell.x, cell.y] = !Map.Obstacles[cell.x, cell.y];
        }

        public void VisualiseSectors(bool visualiseConnections) {
            Visualisation.DrawSectors(Graph.Portals, visualiseConnections);
        }

        public void VisualiseTestPath(int2 start, int2 dest, bool showFlow) {
                       
            var path = PortalPathfinder.FindPortalPath(Graph, start, dest);

            // Visualise the path
            FlowData.InitialiseTo(0);
            if (path.Length > 0) {
                for (int i = 0; i < path.Length; i++) {
                    var node = path[i];
                    var sector = Graph.Costs.Sectors[node.Position.SectorIndex];
                    var flow = FlowCalculationController.RequestCalculation(sector, node.GoalBounds, node.Direction);

                    Visualisation.DrawPortal(node.GoalBounds);
                    if (showFlow) {
                        CopyFlowVisualisationData(flow);
                    } else {
                        Visualisation.DrawPortalLink(path[i].GoalBounds.CentrePoint, path[i + 1].GoalBounds.CentrePoint);
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

        private void CopyFlowVisualisationData(FlowFieldTile flowField) {
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
                return true;
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