using FlowTiles.PortalPaths;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {

    public static class Visualisation {

        public static void DrawSectors (PathableGraph graph) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                if (!graph.SectorIsInitialised(index)) continue;
                var sector = graph.IndexToSector(index);
                DrawRect(sector.Bounds, Color.blue, 0);
            }
        }

        public static void DrawSectorPortals (PathableGraph graph, int travelType) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                if (!graph.SectorIsInitialised(index)) continue;

                var sector = graph.IndexToSectorMap(index, travelType);
                var nodes = sector.Portals.Exits;
                for (int i = 0; i < nodes.Length; i++) {
                    DrawRect(nodes[i].Bounds, Color.red);
                }                
            }
        }

        public static void DrawSectorConnections(PathableGraph graph, int travelType) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                if (!graph.SectorIsInitialised(index)) continue;

                var sector = graph.IndexToSectorMap(index, travelType);
                var nodes = sector.Portals.Exits;
                DrawSectorConnections(nodes);
            }
        }

        private static void DrawSectorConnections(INativeList<Portal> nodes) {
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

        public static void DrawPortalLink(float2 from, float2 to, Color color) {
            Debug.DrawLine(ToVector(from), ToVector(to), color);
        }

        public static void DrawRect(CellRect bounds, Color color, float border = 0.1f) {
            var pos00 = ToVector(bounds.MinCell.x, bounds.MinCell.y) + new Vector3(-0.5f + border, -0.5f + border);
            var pos01 = ToVector(bounds.MinCell.x, bounds.MaxCell.y) + new Vector3(-0.5f + border, 0.5f - border);
            var pos10 = ToVector(bounds.MaxCell.x, bounds.MinCell.y) + new Vector3(0.5f - border, -0.5f + border);
            var pos11 = ToVector(bounds.MaxCell.x, bounds.MaxCell.y) + new Vector3(0.5f - border, 0.5f - border);

            Debug.DrawLine(pos00, pos01, color);
            Debug.DrawLine(pos00, pos10, color);
            Debug.DrawLine(pos11, pos01, color);
            Debug.DrawLine(pos11, pos10, color);
        }

        private static Vector3 ToVector(float2 cell) => new Vector3(cell.x, cell.y);
        private static Vector3 ToVector(float x, float y) => new Vector3(x, y);

    }

}