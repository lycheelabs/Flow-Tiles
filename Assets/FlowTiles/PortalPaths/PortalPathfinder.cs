using FlowTiles.FlowFields;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalPathfinder {

        private PathableGraph Graph;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, PortalEdge> Parents;
        private NativeHashMap<int2, float> GScore;
        private NativePriorityQueue<PathfinderNode> Queue;

        public PortalPathfinder (PathableGraph graph, int capacity, Allocator allocator) {
            Graph = graph;
            Visited = new NativeHashSet<int2>(capacity, allocator);
            Parents = new NativeHashMap<int2, PortalEdge>(capacity, allocator);
            GScore = new NativeHashMap<int2, float>(capacity, allocator);
            Queue = new NativePriorityQueue<PathfinderNode>(capacity, allocator);
        }

        public bool TryFindPath(int2 start, FlowField startField, int2 dest, FlowField destField, int travelType, ref UnsafeList<PortalPathNode> result) {

            var startSector = Graph.CellToIndex(start);
            var destSector = Graph.CellToIndex(dest);
            if (!Graph.SectorIsInitialised(startSector) || !Graph.SectorIsInitialised(destSector)) {
                return false;
            }

            // Find start and end root portals
            var startMap = Graph.CellToSectorMap(start, travelType);
            var destMap = Graph.CellToSectorMap(dest, travelType);
            if (startMap.IsFullyBlocked || destMap.IsFullyBlocked) {
                return false;
            }

            var startCell = new SectorCell(startSector, start);
            var destCell = new SectorCell(destSector, dest);
            var startRoot = startMap.GetRootPortal(start);
            var destRoot = destMap.GetRootPortal(dest);
            var destNode = PortalPathNode.NewDestNode(destCell, destMap.Version);

            // Check whether start and dest portals match
            if (start.Equals(dest)) {
                result.Add(destNode);
                return true;
            }

            // Search for the path through the portal graph
            var path = FindPath(startCell, startRoot, startField, destCell, destRoot, destField, travelType);
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
                        Version = map.Version,
                    });
                }
            }
            result.Add(destNode);
            return true;

        }

        private NativeList<PortalEdge> FindPath(SectorCell start, Portal startRoot, FlowField startField, SectorCell dest, Portal destRoot, FlowField destField, int travelType) {
            Visited.Clear();
            Parents.Clear();
            GScore.Clear();
            Queue.Clear();

            GScore[startRoot.Center.Cell] = 0;

            // Queue start edges
            Visited.Add(start.Cell);
            for (int i = 0; i < startRoot.Edges.Length; i++) {
                var edge = startRoot.Edges[i];
                var localCell = edge.end.Cell - startField.Corner;
                var distance = startField.Distances[localCell.x, localCell.y];
                AddEdge(edge, destRoot, distance, true);
            }
            if (startRoot.IsInSameIsland(destRoot)) {
                // Add edge to destination using flow distance
                var localCell = start.Cell - destField.Corner;
                var distance = destField.Distances[localCell.x, localCell.y];
                var edge = new PortalEdge {
                    start = start,
                    end = destRoot.Center,
                    weight = distance,
                    isExit = true,
                };
                AddEdge(edge, destRoot, distance);
            }

            var destCell = dest.Cell;
            while (!Queue.IsEmpty) {

                // Check if we have reached the destination
                var node = Queue.Dequeue();
                var cell = node.Position;
                if (cell.Equals(destCell)) {
                    return RebuildPath(start, dest);
                }

                // Find the portal that matches this cell
                var found = Graph.CellToSectorMap(cell, travelType).TryGetExitPortal(cell, out var current);
                if (!found) {
                    continue;
                }                

                // Follow the edges...
                Visited.Add(current.Center.Cell);

                if (current.IsInSameIsland(destRoot) && !node.IsStartNode) {

                    // Add edge to destination using flow distance
                    var localCell = cell - destField.Corner;
                    var distance = destField.Distances[localCell.x, localCell.y];
                    var edge = new PortalEdge { 
                        start = current.Center, 
                        end = dest, 
                        weight = distance,
                        isExit = true,
                    };
                    ConsiderEdge(edge, current, destRoot);
                }
                else {

                    // Visit all neighbours through edges going out of node
                    foreach (PortalEdge edge in current.Edges) {
                        ConsiderEdge(edge, current, destRoot);
                    }
                }
            }

            return default;
        }

        private void ConsiderEdge(PortalEdge edge, Portal current, Portal dest) {
            var currentCell = current.Center.Cell;
            var nextCell = edge.end.Cell;

            if (Visited.Contains(nextCell)) {
                return;
            }

            // If new value is not better then do nothing
            float newGCost = GScore[currentCell] + edge.weight;
            if (GScore.TryGetValue(nextCell, out var prevGCost) && newGCost >= prevGCost) {
                return;
            }

            // Otherwise store the new value and add the destination into the queue
            AddEdge(edge, dest, newGCost);
        }

        private void AddEdge (PortalEdge edge, Portal dest, float gCost, bool isStartNode = false) {
            var nextCell = edge.end.Cell;
            var destCell = dest.Center.Cell;

            Parents[nextCell] = edge;
            GScore[nextCell] = gCost;

            var combinedCost = gCost + EuclidianDistance(nextCell, destCell);
            Queue.Enqueue(new PathfinderNode(nextCell, combinedCost, isStartNode));
        }

        private NativeList<PortalEdge> RebuildPath(SectorCell start, SectorCell dest) {
            int2 current = dest.Cell;
            var path = new NativeList<PortalEdge>(Allocator.Temp);
            
            var infLoop = 0;
            while (Parents.TryGetValue(current, out var e)) {
                path.Add(e);
                var parent = e.start.Cell;

                // Stop when returned to start
                if (parent.Equals(start.Cell) || parent.Equals(current)) {
                    break;
                }

                // Fallback - infinite loop checks
                infLoop++;
                if (infLoop > 9999) {
                    UnityEngine.Debug.Log("INFINITE LOOP");
                    break;
                }

                // Iterate back to parent
                current = parent;
            }

            return path;
        }

        private float EuclidianDistance(Portal node1, Portal node2) {
            return math.distance(node1.Center.Cell, node2.Center.Cell);
        }

        private float EuclidianDistance(int2 node1, int2 node2) {
            return math.distance(node1, node2);
        }

    }

}