using FlowTiles.Utils;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    [BurstCompile]
    public struct PortalPathfinder {

        private PathableGraph Graph;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, PortalEdge> Parents;
        private NativeHashMap<int2, float> GScore;
        private NativeMinHeap Queue;

        public PortalPathfinder(PathableGraph graph) : this (graph, 200, Allocator.Temp) {}

        public PortalPathfinder (PathableGraph graph, int heapCapacity, Allocator allocator) {
            Graph = graph;
            Visited = new NativeHashSet<int2>(100, allocator);
            Parents = new NativeHashMap<int2, PortalEdge>(100, allocator);
            GScore = new NativeHashMap<int2, float>(100, allocator);
            Queue = new NativeMinHeap(heapCapacity, allocator);
        }

        public bool TryFindPath(int2 start, int2 dest, ref UnsafeList<PortalPathNode> result) {

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
                return true;
            }

            // Search for the path through the portal graph
            var path = FindPath(startPortal, destCluster).ToArray();
            if (path.Length == 0) {
                return false;
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
            return true;

        }

        private LinkedList<PortalEdge> FindPath(Portal start, Portal destCluster) {
            Visited.Clear();
            Parents.Clear();
            GScore.Clear();
            Queue.Clear();

            GScore[start.Position.Cell] = 0;
            Queue.Push(new MinHeapNode(start.Position.Cell, EuclidianDistance(start, destCluster)));

            while (Queue.HasNext()) {
                var index = Queue.Pop();
                var cell = Queue[index].Position;
                var found = Graph.TryGetExitPortal(cell.x, cell.y, out var current);
                if (!found) {
                    continue;
                }

                Visited.Add(current.Position.Cell);

                if (current.IsInSameCluster(destCluster)) {
                    //Rebuild path and return it
                    return RebuildPath(current);
                }

                else {
                    // Visit all neighbours through edges going out of node
                    foreach (PortalEdge edge in current.Edges) {

                        // Heap at capacity? Fail!
                        if (Queue.IsFull()) {
                            return new LinkedList<PortalEdge>();
                        }

                        ConsiderEdge(edge, current, destCluster);
                    }
                }
            }

            return new LinkedList<PortalEdge>();
        }

        private void ConsiderEdge(PortalEdge edge, Portal current, Portal dest) {
            var nextCell = edge.end.Cell;
            var destCell = dest.Position.Cell;

            if (Visited.Contains(nextCell)) {
                return;
            }

            // If new value is not better then do nothing
            float newGCost = GScore[current.Position.Cell] + edge.weight;
            if (GScore.TryGetValue(nextCell, out var prevGCost) && newGCost >= prevGCost) {
                return;
            }

            // Otherwise store the new value and add the destination into the queue
            Parents[nextCell] = edge;
            GScore[nextCell] = newGCost;
            Queue.Push(new MinHeapNode(nextCell, newGCost + EuclidianDistance(nextCell, destCell)));
        }

        private LinkedList<PortalEdge> RebuildPath(Portal dest) {
            LinkedList<PortalEdge> res = new LinkedList<PortalEdge>();
            int2 current = dest.Position.Cell;

            while (Parents.TryGetValue(current, out var e)) {
                res.AddFirst(e);
                current = e.start.Cell;
            }

            return res;
        }

        private float EuclidianDistance(Portal node1, Portal node2) {
            return math.distance(node1.Position.Cell, node2.Position.Cell);
        }

        private float EuclidianDistance(int2 node1, int2 node2) {
            return math.distance(node1, node2);
        }

    }

}