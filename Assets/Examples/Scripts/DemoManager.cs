using Unity.Collections;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;

namespace FlowTiles.Examples {

    public class DemoManager : MonoBehaviour {

        public int LevelSize = 1000;
        public bool[,] WallMap;

        private Graph Graph;

        void Start() {
            var halfViewedSize = (LevelSize - 1) / 2f;
            Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
            Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);

            WallMap = new bool[LevelSize, LevelSize];
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    if (Random.value < 0.2f) WallMap[i, j] = true;
                }
            }

            var wallData = new NativeArray<bool>(LevelSize * LevelSize, Allocator.Persistent);
            for (int i = 0; i < LevelSize; i++) {
                for (int j = 0; j < LevelSize; j++) {
                    var index = i + j * LevelSize;
                    wallData[index] = WallMap[i, j];
                }
            }

            var levelSetup = new LevelSetup {
                Size = LevelSize,
                Walls = wallData,
            };

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var singleton = em.CreateEntity();
            em.AddComponent<LevelSetup>(singleton);
            em.SetComponentData(singleton, levelSetup);

            var map = Map.CreateMap(WallMap);
            Graph = new Graph(map, 1, 10);
        }

        void Update() {
            /*var nodes = Graph.nodes;
            foreach (var node in nodes.Values) {
                for (int i = 0; i < node.edges.Count; i++) {
                    var edge = node.edges[i];
                    var pos1 = edge.start.pos;
                    var pos2 = edge.end.pos;
                    Debug.DrawLine(
                        new Vector3(pos1.x, pos1.y), 
                        new Vector3(pos2.x, pos2.y),
                        Color.red);
                }
            }*/
            
            var clusters = Graph.C[0];
            for (int c = 0;  c < clusters.Count; c++) {
                var cluster = clusters[c];
                var midPoint = cluster.Boundaries.CentrePoint;
                var nodes = cluster.Nodes;

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
        }

    }

}