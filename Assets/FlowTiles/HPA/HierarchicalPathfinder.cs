using FlowTiles.PortalGraphs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace FlowTiles {

    public static class HierarchicalPathfinder {

        public static List<int2> FindPortalPath(PortalGraph graph, int2 start, int2 dest) {
            var result = new List<int2>();

            //1. Find start and end nodes
            var startExists = graph.TryGetSectorRoot(start.x, start.y, out var startNode);
            var destExists = graph.TryGetSectorRoot(dest.x, dest.y, out var destNode);
            if (!startExists || !destExists || startNode.pos.Equals(destNode.pos)) {
                return result;
            }

            //2. Search for the path through the portal graph
            var path = AstarPathfinder.FindPath(startNode, destNode).ToArray();
            if (path.Length == 0) {
                return result;
            }

            //3. Convert the path into portal coordinates
            for (var i = 0; i < path.Length - 1; i+=2) {
                result.Add(path[i].end.pos);
            }
            result.Add(dest);
            return result;
        }

        public static LinkedList<PortalEdge> FindLowlevelPath(PortalGraph graph, int2 start, int2 dest) {
            /*Portal nStart = graph.portals[start],
                nDest = graph.portals[dest];

            return AstarPathfinder.FindPath(nStart, nDest);*/
            return null;
        }

        public static LinkedList<PortalEdge> GetLayerPathFromHPA(LinkedList<PortalEdge> hpa, int layer) {
            LinkedList<PortalEdge> res = new LinkedList<PortalEdge>();

            //Iterate through all edges as a breadth-first-search on parent-child connections between edges
            //we start at value layers, and add children to the queue while decrementing the layer value.
            //When the layer value is 0, we display it
            Queue<ValueTuple<int, PortalEdge>> queue = new Queue<ValueTuple<int, PortalEdge>>();

            //Add all edges from current level
            foreach (PortalEdge e in hpa)
                queue.Enqueue(new ValueTuple<int, PortalEdge>(layer, e));

            ValueTuple<int, PortalEdge> current;
            while (queue.Count > 0) {
                current = queue.Dequeue();

                if (current.Item1 == 0) {
                    res.AddLast(current.Item2);
                }
                else if (current.Item2.type == PortalEdgeType.INTER) {
                    //No underlying path for intra edges... 
                    //Add the same edge with lower layer
                    queue.Enqueue(new ValueTuple<int, PortalEdge>(current.Item1 - 1, current.Item2));
                }
                else {
                    foreach (PortalEdge e in current.Item2.UnderlyingPath)
                        queue.Enqueue(new ValueTuple<int, PortalEdge>(current.Item1 - 1, e));
                }
            }

            return res;
        }
    }

}