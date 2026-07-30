using LycheeLabs.FlowTiles.PortalPaths;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace LycheeLabs.FlowTiles {

    public static class Visualisation {

        public static void DrawSectors (PathableGraph graph) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                var sector = graph.IndexToSector(index);
                var min = sector.Bounds.MinCell;
                var max = sector.Bounds.MaxCell;

                if (graph.Layout.Rounding > 1) {
                    var roundingCount = graph.Layout.Resolution / graph.Layout.Rounding;
                    for (int r = 0; r < roundingCount; r++) {
                        var offset = (r + 1) * graph.Layout.Rounding;
                        DrawCellLine(
                            new int2(min.x, min.y + offset), 
                            new int2(max.x + 1, min.y + offset), 
                            new Color(1, 1, 1, 0.15f));
                    }
                    for (int c = 0; c < roundingCount; c++) {
                        var offset = (c + 1) * graph.Layout.Rounding;
                        DrawCellLine(
                            new int2(min.x + offset, min.y),
                            new int2(min.x + offset, max.y + 1),
                            new Color(1, 1, 1, 0.15f));
                    }
                }

                DrawCellRect(sector.Bounds, Color.blue, 0);
            }
        }

        public static void DrawSectorPortals (PathableGraph graph, int travelType) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            for (int index = 0; index < numSectors; index++) {
                if (!graph.SectorIsInitialised(index)) continue;

                var sector = graph.IndexToSectorMap(index, travelType);
                var nodes = sector.Portals.Exits;
                for (int i = 0; i < nodes.Length; i++) {
                    DrawCellRect(nodes[i].Bounds, Color.green);
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
                        new Vector3(pos1.x + 0.5f, 0, pos1.y + 0.5f),
                        new Vector3(pos2.x + 0.5f, 0, pos2.y + 0.5f),
                        Color.red);
                }
            }
        }

        public static void DrawPortalLink(float2 from, float2 to) {
            Debug.DrawLine(ToVector(from), ToVector(to), Color.green);
        }

        public static void DrawCellLine (int2 fromCell, int2 toCell, Color color) {
            var pos0 = ToVector(fromCell.x, fromCell.y);
            var pos1 = ToVector(toCell.x, toCell.y);
            Debug.DrawLine(pos0, pos1, color);
        }

        public static void DrawCellRect(CellRect bounds, Color color, float border = 0.1f) {
            var pos00 = ToVector(bounds.MinCell.x, bounds.MinCell.y) + new Vector3(border, 0, +border);
            var pos01 = ToVector(bounds.MinCell.x, bounds.MaxCell.y + 1) + new Vector3(border, 0, -border);
            var pos10 = ToVector(bounds.MaxCell.x + 1, bounds.MinCell.y) + new Vector3(-border, 0, +border);
            var pos11 = ToVector(bounds.MaxCell.x + 1, bounds.MaxCell.y + 1) + new Vector3(-border, 0, -border);

            Debug.DrawLine(pos00, pos01, color);
            Debug.DrawLine(pos00, pos10, color);
            Debug.DrawLine(pos11, pos01, color);
            Debug.DrawLine(pos11, pos10, color);
        }

        private static Vector3 ToVector(float2 cell) => new Vector3(cell.x, 0, cell.y);
        private static Vector3 ToVector(float x, float y) => new Vector3(x, 0, y);

    }

}