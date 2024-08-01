using FlowTiles.FlowField;
using FlowTiles.PortalGraphs;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

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

        private Map Map;
        private NativeArray<float4> ColorData;
        private NativeArray<float2> FlowData;
        private PortalGraph Graph;

        void Start() {

            // Initialise the map
            Map = new Map(LevelSize, LevelSize);
            Map.InitialiseRandomObstacles();

            // Create the graph
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Graph = new PortalGraph(Map.Bounds.Size, Resolution);
            //Graph.Build(Map);
            PortalGraph.StaticBuild(ref Graph, ref Map);
            stopwatch.Stop();
            Debug.Log(string.Format ("Portal graph created in: {0} ms", (int)stopwatch.Elapsed.TotalMilliseconds));

            // Allocate visualisation data
            ColorData = new NativeArray<float4>(LevelSize * LevelSize, Allocator.Persistent);
            FlowData = new NativeArray<float2>(LevelSize * LevelSize, Allocator.Persistent);

            for (int y = 0; y < LevelSize; y++) {
                for (int x = 0; x < LevelSize; x++) {
                    var index = x + y * LevelSize;
                    var sector = Graph.GetSector(x, y);
                    var color = sector.Colors.GetColor(x % Resolution, y % Resolution);
                    
                    ColorData[index] = 1f;
                    if (color > 0) {
                        ColorData[index] = graphColorings[(color - 1) % graphColorings.Length];
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

            // Position the camera
            var halfViewedSize = (LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);

        }

        void Update() {

            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {

                // Update the agent
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                var agents = em.CreateEntityQuery(new ComponentType[] { typeof(AgentData) });
                if (agents.TryGetSingletonEntity<AgentData>(out Entity agent)) {
                    em.SetComponentData(agent, new PathfindingData {
                        OriginCell = 0,
                        DestCell = mouseCell
                    });
                }

                // Modify the grid
                if (Input.GetMouseButtonDown(0)) {
                    var cellIndex = mouseCell.x + mouseCell.y * LevelSize;
                    var flip = !Map.Obstacles[cellIndex];
                    Map.Obstacles[cellIndex] = flip;
                }

                for (int i = 0; i < LevelSize * LevelSize; i++) {
                    FlowData[i] = 0;
                }

                // Find a path
                var start = new int2(0, 0);
                var dest = mouseCell;
                var path = PortalPathfinder.FindPortalPath(Graph, start, dest);

                if (path.Length > 0) {

                    for (int i = 0; i < path.Length; i++) {
                        var node = path[i];
                        var sector = Graph.sectors[node.Position.SectorIndex];

                        var flow = FlowCalculationController.RequestCalculation(sector, node.GoalBounds, node.Direction);

                        //DrawPortalLink(path[i].Cell, path[i + 1].Cell);
                        DrawPortal(node.GoalBounds);
                        VisualiseFlowField(sector, node.Color, flow);

                    }

                }

            }

            // Visualise the graph
            var clusters = Graph.sectors;
            for (int c = 0;  c < clusters.Length; c++) {
                var cluster = clusters[c];
                var nodes = cluster.ExitPortals;

                DrawClusterBoundaries(cluster);
                if (VisualiseConnections) {
                    DrawClusterConnections(nodes);
                }
            }
        }

        private void VisualiseFlowField(Sector sector, int color, FlowFieldTile flowField) {
            var bounds = sector.Bounds;
            for (int x = 0; x < bounds.Size.x; x++) {
                for (int y = 0; y < bounds.Size.y; y++) {
                    var mapIndex = (x + bounds.Min.x) + (y + bounds.Min.y) * LevelSize;
                    var flow = flowField.GetFlow(x, y);
                    var cellColor = sector.Colors.GetColor(x, y);
                    if (FlowData[mapIndex].Equals(new float2(0)) || cellColor == color) {
                        FlowData[mapIndex] = flow;
                    }
                }
            }
        }

        private static void DrawClusterConnections(INativeList<Portal> nodes) {
            for (int n = 0; n < nodes.Length; n++) { 
                var node = nodes[n];
                for (int e = 0; e < node.Edges.Length; e++) {
                    var edge = node.Edges[e];
                    var pos1 = edge.start.Cell;
                    var pos2 = edge.end.Cell;
                    Debug.DrawLine(
                        new Vector3(pos1.x, pos1.y),
                        new Vector3(pos2.x, pos2.y),
                        Color.red);
                }
            }
        }

        private static void DrawClusterBoundaries(Sector cluster) {
            Debug.DrawLine(
                new Vector3(cluster.Bounds.Min.x - 0.5f, cluster.Bounds.Min.y - 0.5f),
                new Vector3(cluster.Bounds.Max.x + 0.5f, cluster.Bounds.Min.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.Min.x - 0.5f, cluster.Bounds.Min.y - 0.5f),
                new Vector3(cluster.Bounds.Min.x - 0.5f, cluster.Bounds.Max.y + 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.Max.x + 0.5f, cluster.Bounds.Max.y + 0.5f),
                new Vector3(cluster.Bounds.Max.x + 0.5f, cluster.Bounds.Min.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.Max.x + 0.5f, cluster.Bounds.Max.y + 0.5f),
                new Vector3(cluster.Bounds.Min.x - 0.5f, cluster.Bounds.Max.y + 0.5f),
                Color.blue);
        }

        private static void DrawPortalLink(int2 from, int2 to) {
            Debug.DrawLine(ToVector(from), ToVector(to), Color.green);
        }

        private static void DrawPortal (Boundaries bounds) {
            var pos00 = ToVector(bounds.Min.x, bounds.Min.y) + new Vector3(-0.4f, -0.4f);
            var pos01 = ToVector(bounds.Min.x, bounds.Max.y) + new Vector3(-0.4f, 0.4f);
            var pos10 = ToVector(bounds.Max.x, bounds.Min.y) + new Vector3(0.4f, -0.4f);
            var pos11 = ToVector(bounds.Max.x, bounds.Max.y) + new Vector3(0.4f, 0.4f); 

            Debug.DrawLine(pos00, pos01, Color.green);
            Debug.DrawLine(pos00, pos10, Color.green);
            Debug.DrawLine(pos11, pos01, Color.green);
            Debug.DrawLine(pos11, pos10, Color.green);
        }

        private static Vector3 ToVector(int2 cell) => new Vector3(cell.x, cell.y);
        private static Vector3 ToVector(int x, int y) => new Vector3(x, y);

    }

}