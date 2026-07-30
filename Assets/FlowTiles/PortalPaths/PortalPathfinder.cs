using LycheeLabs.FlowTiles.FlowFields;
using LycheeLabs.FlowTiles.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.PortalPaths {

    public struct PortalPathfinder {

        private PathableGraph Graph;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, PortalEdge> Parents;
        private NativeHashMap<int2, float> GScore;
        private NativePriorityQueue<PathfinderNode> Queue;

        private bool isCreated;

        public PortalPathfinder (PathableGraph graph, int capacity, Allocator allocator) {
            Graph = graph;
            Visited = new NativeHashSet<int2>(capacity, allocator);
            Parents = new NativeHashMap<int2, PortalEdge>(capacity, allocator);
            GScore = new NativeHashMap<int2, float>(capacity, allocator);
            Queue = new NativePriorityQueue<PathfinderNode>(capacity, allocator);
            isCreated = true;
        }

        public bool IsCreated => isCreated;

        public void Dispose () {
            if (isCreated) {
                isCreated = false;

                if (Visited.IsCreated) Visited.Dispose();
                if (Parents.IsCreated) Parents.Dispose();
                if (GScore.IsCreated) GScore.Dispose();
                if (Queue.IsCreated) Queue.Dispose();

                Visited = default;
                Parents = default;
                GScore = default;
                Queue = default;
            }
        }

        public bool TryFindPath(int2 start, FlowField startField, int2 dest, FlowField destField, int travelType, ref UnsafeList<PortalPathNode> result) {
            if (!IsCreated) {
                return false;
            }
            if (!result.IsCreated) {
                UnityEngine.Debug.LogError("PortalPathfinder: Result UnsafeList is not created");
                return false;
            }

            var startSector = Graph.CellToIndex(start);
            var destSector = Graph.CellToIndex(dest);
            if (!Graph.SectorIsInitialised(startSector) || !Graph.SectorIsInitialised(destSector)) {
                return false;
            }

            // Find start and end root portals
            var startMap = Graph.CellToSectorMap(start, travelType);
            var startCell = new SectorCell(startSector, start);
            var startRoot = startMap.GetRoot(start);

            var destMap = Graph.CellToSectorMap(dest, travelType);
            var destCell = new SectorCell(destSector, dest);
            var destRoot = destMap.GetRoot(dest);

            // Check whether no path can exist
            if (startRoot.Continent != destRoot.Continent) {
                return false;
            }

            // Check whether start and dest portals match
            if (start.x == dest.x && start.y == dest.y) {
                result.Add(PortalPathNode.NewDestNode(destCell, destMap.Version));
                return true;
            }

            // Search for the path through the portal graph
            var pathEdges = new NativeList<PortalEdge>(Allocator.Temp);
            var pathFound = TryFindPath(startCell, startRoot, startField, destCell, destRoot, destField, travelType, ref pathEdges);
            if (pathFound) {
                
                // Convert the sector-spanning edges into PortalPathNodes
                for (var i = pathEdges.Length - 1; i >= 0; i--) {
                    var edge = pathEdges[i];
                    if (edge.SpansTwoSectors) {
                        var map = Graph.IndexToSectorMap(edge.start.SectorIndex, travelType);
                        var portal = map.GetPortal(edge.start.Cell);
                        result.Add(new PortalPathNode {
                            Position = edge.start,
                            GoalBounds = portal.Bounds,
                            Direction = edge.Span,
                            Version = map.Version,
                        });
                    }
                }
                result.Add(PortalPathNode.NewDestNode(destCell, destMap.Version));

            }

            pathEdges.Dispose();
            return pathFound;

        }
        
        private bool TryFindPath(SectorCell start, SectorRoot startRoot, FlowField startField, SectorCell dest, SectorRoot destRoot, FlowField destField, int travelType, ref NativeList<PortalEdge> path) {
            Visited.Clear();
            Parents.Clear();
            GScore.Clear();
            Queue.Clear();

            path.Clear();

            // Queue start edges to exit portals
            Visited.Add(start.Cell);
            for (int i = 0; i < startRoot.Portals.Length; i++) {
                var portalCell = startRoot.Portals[i];
                var localCell = portalCell.Cell - startField.Corner;
                
                if (!startField.Distances.IsValidIndex(localCell.x, localCell.y)) {
                    continue;
                }
                var distance = startField.Distances[localCell.x, localCell.y];
                var startEdge = new PortalEdge {
                    start = start,
                    end = portalCell,
                    weight = distance,
                };
                AddEdge(startEdge, dest, distance, true);
            }

            // Queue start edge directly to destination (if roots match)
            if (startRoot.Matches(destRoot)) {
                var localCell = start.Cell - destField.Corner;
                
                if (!destField.Distances.IsValidIndex(localCell.x, localCell.y)) {
                    // Skip this edge if coordinates are invalid
                } else {
                    var distance = destField.Distances[localCell.x, localCell.y];
                    var directEdge = new PortalEdge {
                        start = start,
                        end = dest,
                        weight = distance,
                    };
                    AddEdge(directEdge, dest, distance);
                }
            }

            // Search for a path...
            while (!Queue.IsEmpty) {

                // Terminate when we reach the destination
                var node = Queue.Dequeue();
                var cell = node.Position;
                if (cell.x == dest.Cell.x && cell.y == dest.Cell.y) {
                    RebuildPath(start, dest, ref path);
                    return path.Length > 0;
                }

                // Find the portal corresponding to this cell
                var found = Graph.CellToSectorMap(cell, travelType).TryGetPortal(cell, out var current);
                if (!found) {
                    continue;
                }
                Visited.Add(current.Center.Cell);

                // Queue dest edge directly to destination (if island matches)
                if (destRoot.ConnectsToPortal(current) && !node.IsStartNode) {
                    var localCell = cell - destField.Corner;
                    
                    if (destField.Distances.IsValidIndex(localCell.x, localCell.y)) {
                        var distance = destField.Distances[localCell.x, localCell.y];
                        var edge = new PortalEdge { 
                            start = current.Center, 
                            end = dest, 
                            weight = distance,
                        };
                        ConsiderEdge(edge, current, dest);
                    }
                }

                // Consider all neighbor edges
                else {
                    foreach (PortalEdge edge in current.Edges) {
                        ConsiderEdge(edge, current, dest);
                    }
                }
            }

            return false;
        }

        private void ConsiderEdge(PortalEdge edge, Portal current, SectorCell dest) {
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

        private void AddEdge (PortalEdge edge, SectorCell dest, float gCost, bool isStartNode = false) {
            var nextCell = edge.end.Cell;
            var destCell = dest.Cell;

            Parents[nextCell] = edge;
            GScore[nextCell] = gCost;

            var combinedCost = gCost + EuclidianDistance(nextCell, destCell);
            Queue.Enqueue(new PathfinderNode(nextCell, combinedCost, isStartNode));
        }

        private void RebuildPath(SectorCell start, SectorCell dest, ref NativeList<PortalEdge> path) {
            int2 current = dest.Cell;
            var infLoop = 0;

            while (Parents.TryGetValue(current, out var e)) {
                var previous = e.start.Cell;
                path.Add(e);

                // Stop when returned to start
                if (current.Equals(start.Cell)) {
                    break; 
                }

                // Fallback - infinite loop checks
                infLoop++;
                if (previous.Equals(current) || infLoop > 9999) {
                    path.Clear();
                    break;
                }

                // Iterate backwards down the path
                current = previous;
            }
        }

        private float EuclidianDistance(int2 node1, int2 node2) {
            return math.distance(node1, node2);
        }

    }

}