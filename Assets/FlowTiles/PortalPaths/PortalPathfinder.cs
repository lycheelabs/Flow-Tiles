using FlowTiles.PortalGraphs;
using Priority_Queue;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles {

    public struct PortalPathfinder {

        private PathableGraph Graph;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, PortalEdge> Parent;
        private NativeHashMap<int2, float> GScore;
        private SimplePriorityQueue<Portal, float> Queue;

        public PortalPathfinder (PathableGraph graph, Allocator allocator) {
            Graph = graph;
            Visited = new NativeHashSet<int2>(100, allocator);
            Parent = new NativeHashMap<int2, PortalEdge>(100, allocator);
            GScore = new NativeHashMap<int2, float>(100, allocator);
            Queue = new SimplePriorityQueue<Portal, float>();
        }

        public void Dispose () {
            Visited.Dispose();
            Parent.Dispose();
            GScore.Dispose();
        } 

        public UnsafeList<PortalPathNode> FindPortalPath(int2 start, int2 dest) {
            var result = new UnsafeList<PortalPathNode>(32, Allocator.Persistent);

            // Find start and end clusters
            var startPortal = Graph.GetRootPortal(start.x, start.y);
            var destCluster = Graph.GetRootPortal(dest.x, dest.y);
            if (Graph.TryGetExitPortal(start.x, start.y, out var exit)) {
                startPortal = exit;
            }

            // Check whether start and dest clusters match
            var destNode = PortalPathNode.NewDestNode(destCluster, dest);
            if (startPortal.IsInSameCluster(destCluster)) {
                result.Add(destNode);
                return result;
            }

            // Search for the path through the portal graph
            var path = FindPath(startPortal, destCluster).ToArray();
            if (path.Length == 0) {
                return result;
            }

            // Convert the sector-spanning edges into PortalPathNodes
            for (var i = 0; i < path.Length; i++) {
                var edge = path[i];
                if (edge.SpansTwoSectors) {
                    var sector = Graph.Portals.Sectors[edge.start.SectorIndex];
                    var portal = sector.GetExitPortalAt(edge.start.Cell);
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

        private LinkedList<PortalEdge> FindPath(Portal start, Portal destCluster) {
            
            GScore[start.Position.Cell] = 0;
            Queue.Enqueue(start, EuclidianDistance(start, destCluster));
            Portal current;

            while (Queue.Count > 0) {
                current = Queue.Dequeue();
                Visited.Add(current.Position.Cell);

                if (current.IsInSameCluster(destCluster)) {
                    //Rebuild path and return it
                    return RebuildPath(current);
                }

                else {
                    // Visit all neighbours through edges going out of node
                    foreach (PortalEdge edge in current.Edges) {
                        var nextSector = Graph.Portals.Sectors[edge.end.SectorIndex];
                        var next = nextSector.GetExitPortalAt(edge.end.Cell);
                        ConsiderEdge(edge, current, next, destCluster);
                    }
                }
            }

            return new LinkedList<PortalEdge>();
        }

        private void ConsiderEdge(PortalEdge edge, Portal current, Portal next, Portal dest) {
            if (Visited.Contains(edge.end.Cell)) {
                return;
            }

            // If new value is not better then do nothing
            float newGCost = GScore[current.Position.Cell] + edge.weight;
            if (GScore.TryGetValue(edge.end.Cell, out var prevGCost) && newGCost >= prevGCost) {
                return;
            }

            // Otherwise store the new value and add the destination into the queue
            Parent[edge.end.Cell] = edge;
            GScore[edge.end.Cell] = newGCost;
            Queue.Enqueue(next, newGCost + EuclidianDistance(next, dest));
        }

        private LinkedList<PortalEdge> RebuildPath(Portal dest) {
            LinkedList<PortalEdge> res = new LinkedList<PortalEdge>();
            int2 current = dest.Position.Cell;

            while (Parent.TryGetValue(current, out var e)) {
                res.AddFirst(e);
                current = e.start.Cell;
            }

            return res;
        }

        private float EuclidianDistance(Portal node1, Portal node2) {
            return math.distance(node1.Position.Cell, node2.Position.Cell);
        }

    }

}