using FlowTiles.PortalGraphs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

        private static float4[] graphColorings = new float4[] { 
            new float4(0.8f, 1f, 0.8f, 1),
            new float4(0.8f, 0.8f, 1f, 1),
            new float4(1f, 1f, 0.8f, 1),
            new float4(0.8f, 1f, 1f, 1),
            new float4(1f, 0.8f, 1f, 1),
            new float4(1f, 0.8f, 0.8f, 1),
        };

        // -----------------------------------------

        public int LevelSize = 1000;
        public int Resolution = 10;
        public bool VisualiseConnections;

        private bool[,] WallMap;
        private NativeArray<bool> WallData;
        private NativeArray<float4> ColorData;
        private PortalGraph Graph;


        void Start() {
            var halfViewedSize = (LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);

            WallMap = new bool[LevelSize, LevelSize];
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    if (UnityEngine.Random.value < 0.2f) WallMap[i, j] = true;
                }
            }

            WallData = new NativeArray<bool>(LevelSize * LevelSize, Allocator.Persistent);
            ColorData = new NativeArray<float4>(LevelSize * LevelSize, Allocator.Persistent);
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    if (i == 0 && j == 0) continue;
                    var index = i + j * LevelSize;
                    WallData[index] = WallMap[i, j];
                }
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(singleton);
            em.SetComponentData(singleton, new LevelSetup {
                Size = LevelSize,
                Walls = WallData,
                Colors = ColorData,
            });

            var map = Map.CreateMap(WallMap);
            Graph = new PortalGraph(map, Resolution);

            for (int y = 0; y < LevelSize; y++) {
                for (int x = 0; x < LevelSize; x++) {
                    var sector = Graph.GetSector(x, y);
                    var color = sector.Colors.GetColor(x % Resolution, y % Resolution);
                    var index = x + y * LevelSize;
                    if (color > 0) {
                        ColorData[index] = graphColorings[(color - 1) % graphColorings.Length];
                    }
                }
            }

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
                    var flip = !WallMap[mouseCell.x, mouseCell.y];
                    WallData[cellIndex] = flip;
                    WallMap[mouseCell.x, mouseCell.y] = flip;
                }

                // Find a path
                var start = new int2(0, 0);
                var dest = mouseCell;
                var path = PortalPathfinder.FindPortalPath(Graph, start, dest);

                // Visualise the path
                if (path.Count > 0) {
                    DrawPortalLink(new Vector3(start.x, start.y), new Vector3(path[0].x, path[0].y));
                    for (int i = 0; i < path.Count - 1; i++) {
                        DrawPortalLink(new Vector3(path[i].x, path[i].y),new Vector3(path[i + 1].x, path[i + 1].y));
                    }
                }

            }

            // Visualise the graph
            var clusters = Graph.sectors;
            for (int c = 0;  c < clusters.Length; c++) {
                var cluster = clusters[c];
                var nodes = cluster.EdgePortals;

                DrawClusterBoundaries(cluster);
                if (VisualiseConnections) {
                    DrawClusterConnections(nodes);
                }
            }
        }

        private static void DrawClusterConnections(Dictionary<int2, Portal> nodes) {
            foreach (var node in nodes.Values) {
                for (int e = 0; e < node.edges.Length; e++) {
                    var edge = node.edges[e];
                    var pos1 = edge.startCell;
                    var pos2 = edge.endCell;
                    Debug.DrawLine(
                        new Vector3(pos1.x, pos1.y),
                        new Vector3(pos2.x, pos2.y),
                        Color.red);
                }
            }
        }

        private static void DrawClusterBoundaries(PortalGraphSector cluster) {
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Min.x - 0.5f, cluster.Boundaries.Min.y - 0.5f),
                new Vector3(cluster.Boundaries.Max.x + 0.5f, cluster.Boundaries.Min.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Min.x - 0.5f, cluster.Boundaries.Min.y - 0.5f),
                new Vector3(cluster.Boundaries.Min.x - 0.5f, cluster.Boundaries.Max.y + 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Max.x + 0.5f, cluster.Boundaries.Max.y + 0.5f),
                new Vector3(cluster.Boundaries.Max.x + 0.5f, cluster.Boundaries.Min.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Max.x + 0.5f, cluster.Boundaries.Max.y + 0.5f),
                new Vector3(cluster.Boundaries.Min.x - 0.5f, cluster.Boundaries.Max.y + 0.5f),
                Color.blue);
        }

        private static void DrawPortalLink (Vector3 pos1, Vector3 pos2) {
            Debug.DrawLine(pos1, pos2, Color.green);

            Debug.DrawLine(pos2 + new Vector3(-0.4f, -0.4f), pos2 + new Vector3(0.4f, -0.4f), Color.green);
            Debug.DrawLine(pos2 + new Vector3(-0.4f, -0.4f), pos2 + new Vector3(-0.4f, 0.4f), Color.green);
            Debug.DrawLine(pos2 + new Vector3(0.4f, 0.4f), pos2 + new Vector3(0.4f, -0.4f), Color.green);
            Debug.DrawLine(pos2 + new Vector3(0.4f, 0.4f), pos2 + new Vector3(-0.4f, 0.4f), Color.green);
        }

    }

}