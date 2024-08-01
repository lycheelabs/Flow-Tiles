using FlowTiles.FlowField;
using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {
    public class DemoLevel {

        private const float ColorFading = 0.7f;

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

        private PathableMap Map;
        private UnsafeField<float4> ColorData;
        private UnsafeField<float2> FlowData;
        private PortalGraph Graph;

        public DemoLevel (int levelSize, int resolution) {
            LevelSize = levelSize;
            Resolution = resolution;

            // Initialise the map
            Map = new PathableMap(LevelSize, LevelSize);
            Map.InitialiseRandomObstacles();

            // Create the graph
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Graph = new PortalGraph(Map.Bounds.SizeCells, Resolution);
            PortalGraph.BurstBuild(ref Graph, ref Map);
            stopwatch.Stop();
            Debug.Log(string.Format("Portal graph created in: {0} ms", (int)stopwatch.Elapsed.TotalMilliseconds));

            // Allocate visualisation data
            ColorData = new UnsafeField<float4>(LevelSize, Allocator.Persistent, initialiseTo: 1);
            FlowData = new UnsafeField<float2>(LevelSize, Allocator.Persistent);

            for (int y = 0; y < LevelSize; y++) {
                for (int x = 0; x < LevelSize; x++) {
                    var colors = Graph.GetColorField(x, y);
                    var color = colors.GetColor(x % Resolution, y % Resolution);
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

        public void FlipWallAt(int2 cell) {
            Map.Obstacles[cell.x, cell.y] = !Map.Obstacles[cell.x, cell.y];
        }

        public void VisualiseSectors(bool visualiseConnections) {
            Visualisation.DrawSectors(Graph, visualiseConnections);
        }

        public void VisualiseTestPath(object value, int2 mouseCell, bool showFlow) {
            FlowData.InitialiseTo(0);

            // Find a path
            var start = new int2(0, 0);
            var dest = mouseCell;
            var path = PortalPathfinder.FindPortalPath(Graph, start, dest);

            // Visualise the path
            if (path.Length > 0) {
                for (int i = 0; i < path.Length; i++) {
                    var node = path[i];
                    var sector = Graph.MapSectors[node.Position.SectorIndex];

                    var flow = FlowCalculationController.RequestCalculation(sector, node.GoalBounds, node.Direction);

                    Visualisation.DrawPortal(node.GoalBounds);
                    if (showFlow) {
                        CopyFlowVisualisationData(sector, node.Color, flow);
                    } else {
                        Visualisation.DrawPortalLink(path[i].GoalBounds.CentrePoint, path[i + 1].GoalBounds.CentrePoint);
                    }

                }
            }

        }

        private void CopyFlowVisualisationData(MapSector sector, int color, FlowFieldTile flowField) {
            var bounds = sector.Bounds;
            for (int x = 0; x < bounds.SizeCells.x; x++) {
                for (int y = 0; y < bounds.SizeCells.y; y++) {
                    var mapCell = new int2(x + bounds.MinCell.x, y + bounds.MinCell.y);
                    var flow = flowField.GetFlow(x, y);
                    var cellColor = sector.Colors.GetColor(x, y);
                    if (FlowData[mapCell.x, mapCell.y].Equals(new float2(0)) || cellColor == color) {
                        FlowData[mapCell.x, mapCell.y] = flow;
                    }
                }
            }
        }

    }

}