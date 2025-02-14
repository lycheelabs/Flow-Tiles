using System.Diagnostics;
using Unity.Collections;

namespace FlowTiles.PortalPaths {

    public struct ContinentPathfinder {

        public NativeQueue<Portal> QueuedNodes;

        public void Dispose () {
            QueuedNodes.Dispose();
        }

        public void RecalculateContinents(ref PathableGraph graph) {
            var numTravelType = graph.NumTravelTypes;
            for (int t = 0; t < numTravelType; t++) {
                RecalculateContinents(ref graph, t);
            }
        }

        private void RecalculateContinents(ref PathableGraph graph, int travelType) {
            ClearContinents(ref graph, travelType);
            FindContinents(ref graph, travelType);
        }

        private static PathableGraph ClearContinents(ref PathableGraph graph, int travelType) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            
            for (int s = 0; s < numSectors; s++) {
                var sector = graph.IndexToSector(s);
                var portals = sector.GetData(travelType).Portals;
                for (int r = 0; r < portals.Roots.Length; r++) {
                    var root = portals.Roots[r];
                    root.Continent = 0;
                    portals.Roots[r] = root;
                }
                for (int r = 0; r < portals.Exits.Length; r++) {
                    var exit = portals.Exits[r];
                    exit.Continent = 0;
                    portals.Exits[r] = exit;
                }
            }

            return graph;
        }

        private PathableGraph FindContinents(ref PathableGraph graph, int travelType) {
            var numSectors = graph.Layout.NumSectorsInLevel;
            int continent = 1;

            var infLoopCheck = 0;
            while (true) {

                // Start a new continent at the next un-set node
                var foundUnsetNode = false;
                for (int s = 0; s < numSectors; s++) {
                    var sector = graph.IndexToSector(s);
                    var portals = sector.GetData(travelType).Portals;
                    for (int r = 0; r < portals.Roots.Length; r++) {
                        var root = portals.Roots[r];
                        if (root.Continent <= 0) {
                            FindContinentStartingAt(graph, root, continent, travelType);
                            continent++;
                            foundUnsetNode = true;
                        }
                    }
                }

                if (!foundUnsetNode) {
                    break;
                }

                infLoopCheck++;
                if (infLoopCheck > 9999) {
                    UnityEngine.Debug.Log("Infinite loop detected and broken");
                    break;
                }
            }
            
            return graph;
        }

        private void FindContinentStartingAt(PathableGraph graph, SectorRoot start, int continent, int travelType) {

            // Update the root
            var startSector = graph.IndexToSectorMap(start.SectorIndex, travelType);
            start.Continent = continent;
            startSector.Portals.Roots[start.Island - 1] = start;

            // Update and queue the root's portals
            QueuedNodes.Clear();
            for (int p = 0; p < start.Portals.Length; p++) {
                var cell = start.Portals[p].Cell;              
                var portal = startSector.GetPortal(cell);
                portal.Continent = continent;
                startSector.SetPortal(cell, portal);
                QueuedNodes.Enqueue(portal);
            }

            // Search through all connected nodes
            var infLoopCheck = 0;
            while (QueuedNodes.Count > 0) {
                var node = QueuedNodes.Dequeue();
                node.Continent = continent;

                for (int i = 0; i < node.Edges.Length;i++) {
                    var edge = node.Edges[i];
                    var cell = edge.end.Cell;
                    var sector = graph.IndexToSectorMap(edge.end.SectorIndex, travelType);
                    var exit = sector.GetPortal(cell);

                    if (exit.Continent <= 0) {

                        // Update this portal
                        exit.Continent = continent;
                        sector.SetPortal(cell, exit);
                        QueuedNodes.Enqueue(exit);

                        // Update this portal's root
                        var root = sector.Portals.Roots[exit.Island - 1];
                        root.Continent = continent;
                        sector.Portals.Roots[exit.Island - 1] = root;

                    }
                }

                infLoopCheck++;
                if (infLoopCheck > 9999) {
                    UnityEngine.Debug.Log("Infinite loop detected and broken");
                    break;
                }
            }
        }

    }

}