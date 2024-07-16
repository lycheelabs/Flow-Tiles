using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

        public int LevelSize = 1000;
        public bool[,] WallMap;
        public NativeArray<bool> WallData;

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
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    var index = i + j * LevelSize;
                    WallData[index] = WallMap[i, j];
                }
            }

            var levelSetup = new LevelSetup {
                Size = LevelSize,
                Walls = WallData,
            };

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(singleton);
            em.SetComponentData(singleton, levelSetup);

            var map = Map.CreateMap(WallMap);
            Graph = new PortalGraph(map, 10);
        }

        void Update() {

            // Modify the grid
            var position = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
            var mouseCell = new int2((int)(position.x + 0.5f), (int)(position.y + 0.5f));
            if (mouseCell.x >= 0 && mouseCell.y >= 0 && mouseCell.x < LevelSize && mouseCell.y < LevelSize) {
                if (Input.GetMouseButtonDown(0)) {
                    var cellIndex = mouseCell.x + mouseCell.y * LevelSize;
                    var flip = !WallMap[mouseCell.x, mouseCell.y];
                    WallData[cellIndex] = flip;
                    WallMap[mouseCell.x, mouseCell.y] = flip;
                }
            }

            // Visualise the graph
            var clusters = Graph.sectors;
            for (int c = 0;  c < clusters.Count; c++) {
                var cluster = clusters[c];
                var nodes = cluster.Portals;

                DrawClusterConnections(nodes);
                DrawClusterBoundaries(cluster);
            }
        }

        private static void DrawClusterConnections(Dictionary<GridTile, Portal> nodes) {
            foreach (var node in nodes.Values) {
                for (int e = 0; e < node.edges.Count; e++) {
                    var edge = node.edges[e];
                    var pos1 = edge.start.pos;
                    var pos2 = edge.end.pos;
                    Debug.DrawLine(
                        new Vector3(pos1.x, pos1.y),
                        new Vector3(pos2.x, pos2.y),
                        Color.red);
                }
            }
        }

        private static void DrawClusterBoundaries(PortalGraphSector cluster) {
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Min.x, cluster.Boundaries.Min.y),
                new Vector3(cluster.Boundaries.Max.x, cluster.Boundaries.Min.y),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Min.x, cluster.Boundaries.Min.y),
                new Vector3(cluster.Boundaries.Min.x, cluster.Boundaries.Max.y),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Max.x, cluster.Boundaries.Max.y),
                new Vector3(cluster.Boundaries.Max.x, cluster.Boundaries.Min.y),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Boundaries.Max.x, cluster.Boundaries.Max.y),
                new Vector3(cluster.Boundaries.Min.x, cluster.Boundaries.Max.y),
                Color.blue);
        }
    }

}