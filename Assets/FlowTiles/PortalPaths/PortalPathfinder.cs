using FlowTiles.FlowFields;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

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

            // Find start and end clusters
            var startMap = Graph.CellToSectorMap(start, travelType);
            var destMap = Graph.CellToSectorMap(dest, travelType);
            if (startMap.IsFullyBlocked || destMap.IsFullyBlocked) {
                return false;
            }

            var startRoot = startMap.GetRootPortal(start);
            var destRoot = destMap.GetRootPortal(dest);
            if (Graph.CellToSectorMap(start, travelType).TryGetExitPortal(start, out var exit)) {
                startRoot = exit;
            }

            // Check whether start and dest clusters match
            var destNode = PortalPathNode.NewDestNode(destRoot, dest, destMap.Version);
            if (startRoot.IsInSameIsland(destRoot)) {
                result.Add(destNode);
                return true;
            }

            // Search for the path through the portal graph
            var path = FindPath(startRoot, startField, destRoot, destField, travelType);
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

        private NativeList<PortalEdge> FindPath(Portal start, FlowField startField, Portal destCluster, FlowField destField, int travelType) {
            Visited.Clear();
            Parents.Clear();
            GScore.Clear();
            Queue.Clear();

            GScore[start.Position.Cell] = 0;

            // Queue start edges
            for (int i = 0; i < start.Edges.Length; i++) {
                var edge = start.Edges[i];
                var localCell = edge.end.Cell - startField.Corner;
                var distance = startField.Distances[localCell.x, localCell.y];
                AddEdge(edge, destCluster, distance);
            }

            var destCell = destCluster.Position.Cell;
            var checkedStart = false;

            while (!Queue.IsEmpty) {

                // Check if we have reached the destination
                var cell = Queue.Dequeue().Position;
                if (cell.Equals(destCell)) {
                    UnityEngine.Debug.Log("DEST");
                    return RebuildPath(destCluster);
                }

                // Find the portal that matches this cell
                Portal current;
                if (!checkedStart) {
                    current = start;
                    checkedStart = true;
                } else {
                    var found = Graph.CellToSectorMap(cell, travelType).TryGetExitPortal(cell, out current);
                    if (!found) {
                        continue;
                    }
                }

                // Follow the edges...
                Visited.Add(current.Position.Cell);

                if (current.IsInSameIsland(destCluster)) {

                    // Add edge to destination using flow distance
                    UnityEngine.Debug.Log("DEST CLUSTER");
                    var localCell = cell - destField.Corner;
                    var distance = destField.Distances[localCell.x, localCell.y];
                    var edge = new PortalEdge { 
                        start = current.Position, 
                        end = destCluster.Position, 
                        weight = distance,
                        isExit = true,
                    };
                    ConsiderEdge(edge, destCluster);
                }
                else {

                    // Visit all neighbours through edges going out of node
                    foreach (PortalEdge edge in current.Edges) {
                        ConsiderEdge(edge, destCluster);
                    }
                }
            }

            return default;
        }

        private void ConsiderEdge(PortalEdge edge, Portal dest) {
            var currentCell = edge.start.Cell;
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

        private void AddEdge (PortalEdge edge, Portal dest, float gCost) {
            var nextCell = edge.end.Cell;
            var destCell = dest.Position.Cell;

            Parents[nextCell] = edge;
            GScore[nextCell] = gCost;
            Queue.Enqueue(new PathfinderNode(nextCell, gCost + EuclidianDistance(nextCell, destCell)));
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