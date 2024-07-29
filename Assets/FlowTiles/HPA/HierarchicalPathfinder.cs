using FlowTiles.PortalGraphs;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace FlowTiles {

    public static class HierarchicalPathfinder {

        public static LinkedList<PortalEdge> FindHierarchicalPath(PortalGraph graph, int2 start, int2 dest) {
            Portal nStart, nDest;

            // Validity checks
            if (!graph.portals.ContainsKey(start) || !graph.portals.ContainsKey(dest)) {
                //UnityEngine.Debug.LogWarning("NO PATH FOUND");
                return new LinkedList<PortalEdge>();
            }

            //1. Insert nodes
            graph.InsertPortals(start, dest, out nStart, out nDest);

            LinkedList<PortalEdge> path;
            //2. search for path in the highest level
            path = AstarPathfinder.FindPath(nStart, nDest);

            //3. Remove all created nodes from the graph
            graph.RemoveAddedPortals();

            return path;
        }

        public static LinkedList<PortalEdge> FindLowlevelPath(PortalGraph graph, int2 start, int2 dest) {
            Portal nStart = graph.portals[start],
                nDest = graph.portals[dest];

            return AstarPathfinder.FindPath(nStart, nDest);
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