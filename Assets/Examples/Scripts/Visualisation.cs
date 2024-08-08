using FlowTiles.PortalPaths;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public static class Visualisation {

        public static void DrawSectors (PathableGraph graph, bool showPortalEdges) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                if (!graph.SectorIsInitialised(index)) continue;

                var sector = graph.IndexToSectorMap(index, 0);
                var nodes = sector.Portals.ExitPortals;

                DrawSectorBoundaries(sector.Portals);
                if (showPortalEdges) {
                    DrawSectorConnections(nodes);
                }
            }
        }

        public static void DrawSectorBoundaries(PortalMap cluster) {
            Debug.DrawLine(
                new Vector3(cluster.Bounds.MinCell.x - 0.5f, cluster.Bounds.MinCell.y - 0.5f),
                new Vector3(cluster.Bounds.MaxCell.x + 0.5f, cluster.Bounds.MinCell.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.MinCell.x - 0.5f, cluster.Bounds.MinCell.y - 0.5f),
                new Vector3(cluster.Bounds.MinCell.x - 0.5f, cluster.Bounds.MaxCell.y + 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.MaxCell.x + 0.5f, cluster.Bounds.MaxCell.y + 0.5f),
                new Vector3(cluster.Bounds.MaxCell.x + 0.5f, cluster.Bounds.MinCell.y - 0.5f),
                Color.blue);
            Debug.DrawLine(
                new Vector3(cluster.Bounds.MaxCell.x + 0.5f, cluster.Bounds.MaxCell.y + 0.5f),
                new Vector3(cluster.Bounds.MinCell.x - 0.5f, cluster.Bounds.MaxCell.y + 0.5f),
                Color.blue);
        }

        public static void DrawSectorConnections(INativeList<Portal> nodes) {
            for (int n = 0; n < nodes.Length; n++) {
                var node = nodes[n];
                for (int e = 0; e < node.Edges.Length; e++) {
                    var edge = node.Edges[e];
                    var pos1 = edge.start.Cell;
                    var pos2 = edge.end.Cell;
                    var diff = pos2 - pos1;
                    Debug.DrawLine(
                        new Vector3(pos1.x, pos1.y),
                        new Vector3(pos2.x, pos2.y),
                        Color.red);
                }
            }
        }

        public static void DrawPortalLink(float2 from, float2 to) {
            Debug.DrawLine(ToVector(from), ToVector(to), Color.green);
        }

        public static void DrawPortal(CellRect bounds) {
            var pos00 = ToVector(bounds.MinCell.x, bounds.MinCell.y) + new Vector3(-0.4f, -0.4f);
            var pos01 = ToVector(bounds.MinCell.x, bounds.MaxCell.y) + new Vector3(-0.4f, 0.4f);
            var pos10 = ToVector(bounds.MaxCell.x, bounds.MinCell.y) + new Vector3(0.4f, -0.4f);
            var pos11 = ToVector(bounds.MaxCell.x, bounds.MaxCell.y) + new Vector3(0.4f, 0.4f);

            Debug.DrawLine(pos00, pos01, Color.green);
            Debug.DrawLine(pos00, pos10, Color.green);
            Debug.DrawLine(pos11, pos01, Color.green);
            Debug.DrawLine(pos11, pos10, Color.green);
        }

        private static Vector3 ToVector(float2 cell) => new Vector3(cell.x, cell.y);
        private static Vector3 ToVector(float x, float y) => new Vector3(x, y);

    }

}