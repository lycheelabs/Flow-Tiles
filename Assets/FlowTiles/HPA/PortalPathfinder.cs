using FlowTiles.PortalGraphs;
using NUnit.Framework.Internal;
using Priority_Queue;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles {

    public class PortalPathfinder {

        public struct PortalPathNode {
            public SectorCell Position;
            public Boundaries GoalBounds;
            public int2 Direction;
            public int Color;

            public int4 CacheKey => new int4(Position.Cell, Direction);

            public static PortalPathNode NewDestNode(Portal cluster, int2 cell) {
                return new PortalPathNode {
                    Position = new SectorCell(cluster.Position.SectorIndex, cell),
                    GoalBounds = new Boundaries(cell, cell),
                    Direction = 0,
                    Color = cluster.Color,
                };
            }
        }

        public static List<PortalPathNode> FindPortalPath(PortalGraph graph, int2 start, int2 dest) {
            var result = new List<PortalPathNode>();

            // Find start and end clusters
            var startExists = graph.TryGetSectorRoot(start.x, start.y, out var startCluster);
            var destExists = graph.TryGetSectorRoot(dest.x, dest.y, out var destCluster);
            if (!startExists || !destExists) {
                return result;
            }

            // Check whether start and dest clusters match
            var destNode = PortalPathNode.NewDestNode(destCluster, dest);
            if (startCluster.IsInSameCluster(destCluster)) {
                result.Add(destNode);
                return result;
            }

            // Search for the path through the portal graph
            var path = FindPath(graph, startCluster, destCluster).ToArray();
            if (path.Length == 0) {
                return result;
            }

            // Convert the sector-spanning edges into PortalPathNodes
            for (var i = 0; i < path.Length; i ++) {
                var edge = path[i];
                if (edge.SpansTwoSectors) {
                    var sector = graph.sectors[edge.start.SectorIndex];
                    var portal = sector.EdgePortals[edge.start.Cell];
                    result.Add(new PortalPathNode {
                        Position = edge.start,
                        GoalBounds = portal.Bounds,
                        Direction = edge.Span,
                        Color = portal.Color,
                    });
                }
            }
            result.Add(destNode);
            return result;

        }

        private static LinkedList<PortalEdge> FindPath(PortalGraph graph, Portal startCluster, Portal destCluster) {
            HashSet<int2> Visited = new HashSet<int2>();
            Dictionary<int2, PortalEdge> Parent = new Dictionary<int2, PortalEdge>();
            Dictionary<int2, float> gScore = new Dictionary<int2, float>();

            SimplePriorityQueue<Portal, float> pq = new SimplePriorityQueue<Portal, float>();

            float temp_gCost, prev_gCost;

            gScore[startCluster.Position.Cell] = 0;
            pq.Enqueue(startCluster, EuclidianDistance(startCluster, destCluster));
            Portal current;

            while (pq.Count > 0) {
                current = pq.Dequeue();

                if (current.IsInSameCluster(destCluster)) {
                    //Rebuild path and return it
                    return RebuildPath(Parent, current);
                }

                Visited.Add(current.Position.Cell);

                // Visit all neighbours through edges going out of node
                foreach (PortalEdge e in current.Edges) {
                    var nextSector = graph.sectors[e.end.SectorIndex];
                    var nextPortal = nextSector.EdgePortals[e.end.Cell];

                    // Check if we visited the outer end of the edge
                    if (Visited.Contains(e.end.Cell))
                        continue;

                    temp_gCost = gScore[current.Position.Cell] + e.weight;

                    // If new value is not better then do nothing
                    if (gScore.TryGetValue(e.end.Cell, out prev_gCost) && temp_gCost >= prev_gCost)
                        continue;

                    // Otherwise store the new value and add the destination into the queue
                    Parent[e.end.Cell] = e;
                    gScore[e.end.Cell] = temp_gCost;

                    pq.Enqueue(nextPortal, temp_gCost + EuclidianDistance(nextPortal, destCluster));
                }
            }

            return new LinkedList<PortalEdge>();
        }

        private static bool IsOutOfGrid(int2 pos, Boundaries boundaries) {
            return (pos.x < boundaries.Min.x || pos.x > boundaries.Max.x) ||
                   (pos.y < boundaries.Min.y || pos.y > boundaries.Max.y);
        }

        private static float EuclidianDistance(Portal node1, Portal node2) {
            return EuclidianDistance(node1.Position.Cell, node2.Position.Cell);
        }

        private static float EuclidianDistance(int2 tile1, int2 tile2) {
            return Mathf.Sqrt(Mathf.Pow(tile2.x - tile1.x, 2) + Mathf.Pow(tile2.y - tile1.y, 2));
        }

        //Rebuild edges
        private static LinkedList<PortalEdge> RebuildPath(Dictionary<int2, PortalEdge> Parent, Portal dest) {
            LinkedList<PortalEdge> res = new LinkedList<PortalEdge>();
            int2 current = dest.Position.Cell;

            while (Parent.TryGetValue(current, out var e)) {
                res.AddFirst(e);
                current = e.start.Cell;
            }

            return res;
        }
    }

}