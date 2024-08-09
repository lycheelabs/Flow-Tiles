using FlowTiles.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalPathfinder {

        private PathableGraph Graph;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, PortalEdge> Parents;
        private NativeHashMap<int2, float> GScore;
        private NativeMinHeap Queue;

        public PortalPathfinder(PathableGraph graph) : this (graph, 1000, Allocator.Temp) {}

        public PortalPathfinder (PathableGraph graph, int capacitry, Allocator allocator) {
            Graph = graph;
            Visited = new NativeHashSet<int2>(capacitry, allocator);
            Parents = new NativeHashMap<int2, PortalEdge>(capacitry, allocator);
            GScore = new NativeHashMap<int2, float>(capacitry, allocator);
            Queue = new NativeMinHeap(capacitry, allocator);
        }

        public bool TryFindPath(int2 start, int2 dest, int travelType, ref UnsafeList<PortalPathNode> result) {

            // Find start and end clusters
            var startMap = Graph.CellToSectorMap(start, travelType);
            var destMap = Graph.CellToSectorMap(dest, travelType);
            var startPortal = startMap.GetRootPortal(start);
            var destCluster = destMap.GetRootPortal(dest);
            if (Graph.CellToSectorMap(start, travelType).TryGetExitPortal(start, out var exit)) {
                startPortal = exit;
            }

            // Check whether start and dest clusters match
            var destNode = PortalPathNode.NewDestNode(destCluster, dest, destMap.Version);
            if (startPortal.IsInSameCluster(destCluster)) {
                result.Add(destNode);
                return true;
            }

            // Search for the path through the portal graph
            var path = FindPath(startPortal, destCluster, travelType);
            if (!path.IsCreated || path.Length == 0) {
                return false;
            }

            // Convert the sector-spanning edges into PortalPathNodes
            for (var i = path.Length - 1; i >= 0; i--) {
                var edge = path[i];
                if (edge.SpansTwoSectors) {
                    var map = Graph.IndexToSectorMap(edge.start.SectorIndex, travelType);
                    var portal = map.GetExitPortal(edge.start.Cell);
                    result.Add(new PortalPathNode {
                        Position = edge.start,
                        GoalBounds = portal.Bounds,
                        Direction = edge.Span,
                        Color = portal.Color,
                        Version = map.Version,
                    });
                }
            }
            result.Add(destNode);
            return true;

        }

        private NativeList<PortalEdge> FindPath(Portal start, Portal destCluster, int travelType) {
            Visited.Clear();
            Parents.Clear();
            GScore.Clear();
            Queue.Clear();

            GScore[start.Position.Cell] = 0;
            Queue.Push(new MinHeapNode(start.Position.Cell, EuclidianDistance(start, destCluster)));

            while (Queue.HasNext()) {
                var index = Queue.Pop();
                var cell = Queue[index].Position;
                var found = Graph.CellToSectorMap(cell, travelType).TryGetExitPortal(cell, out var current);
                if (!found) {
                    current = start;
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
                            return default;
                        }

                        ConsiderEdge(edge, current, destCluster);
                    }
                }
            }

            return default;
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

        private NativeList<PortalEdge> RebuildPath(Portal dest) {
            var res = new NativeList<PortalEdge>(Allocator.Temp);
            int2 current = dest.Position.Cell;

            while (Parents.TryGetValue(current, out var e)) {
                res.Add(e);
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