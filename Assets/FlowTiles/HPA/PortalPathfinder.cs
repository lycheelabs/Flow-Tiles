using FlowTiles.PortalGraphs;
using Priority_Queue;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles {

    public class PortalPathfinder {

        public static List<int2> FindPortalPath(PortalGraph graph, int2 start, int2 dest) {
            var result = new List<int2>();

            //1. Find start and end nodes
            var startExists = graph.TryGetSectorRoot(start.x, start.y, out var startNode);
            var destExists = graph.TryGetSectorRoot(dest.x, dest.y, out var destNode);
            if (!startExists || !destExists || startNode == destNode) {
                return result;
            }

            //2. Search for the path through the portal graph
            var path = FindPath(startNode, destNode).ToArray();
            if (path.Length == 0) {
                return result;
            }

            //3. Convert the path into portal coordinates
            for (var i = 0; i < path.Length - 1; i += 2) {
                result.Add(path[i].end.pos);
            }
            result.Add(dest);
            return result;

        }

        private static LinkedList<PortalEdge> FindPath(Portal start, Portal dest, Boundaries boundaries = null) {
            HashSet<int2> Visited = new HashSet<int2>();
            Dictionary<int2, PortalEdge> Parent = new Dictionary<int2, PortalEdge>();
            Dictionary<int2, float> gScore = new Dictionary<int2, float>();

            SimplePriorityQueue<Portal, float> pq = new SimplePriorityQueue<Portal, float>();

            float temp_gCost, prev_gCost;

            gScore[start.pos] = 0;
            pq.Enqueue(start, EuclidianDistance(start, dest));
            Portal current;

            while (pq.Count > 0) {
                current = pq.Dequeue();

                if (current.root != null
                    && current.root.pos.Equals(dest.pos)
                    && current.root.color == dest.color) {
                    //Rebuild path and return it
                    return RebuildPath(Parent, current);
                }

                Visited.Add(current.pos);

                //Visit all neighbours through edges going out of node
                foreach (PortalEdge e in current.edges) {
                    //If we defined boundaries, check if it crosses it
                    if (boundaries != null && IsOutOfGrid(e.end.pos, boundaries))
                        continue;

                    //Check if we visited the outer end of the edge
                    if (Visited.Contains(e.end.pos))
                        continue;

                    temp_gCost = gScore[current.pos] + e.weight;

                    //If new value is not better then do nothing
                    if (gScore.TryGetValue(e.end.pos, out prev_gCost) && temp_gCost >= prev_gCost)
                        continue;

                    //Otherwise store the new value and add the destination into the queue
                    Parent[e.end.pos] = e;
                    gScore[e.end.pos] = temp_gCost;

                    pq.Enqueue(e.end, temp_gCost + EuclidianDistance(e.end, dest));
                }
            }

            return new LinkedList<PortalEdge>();
        }

        private static bool IsOutOfGrid(int2 pos, Boundaries boundaries) {
            return (pos.x < boundaries.Min.x || pos.x > boundaries.Max.x) ||
                   (pos.y < boundaries.Min.y || pos.y > boundaries.Max.y);
        }

        private static float EuclidianDistance(Portal node1, Portal node2) {
            return EuclidianDistance(node1.pos, node2.pos);
        }

        private static float EuclidianDistance(int2 tile1, int2 tile2) {
            return Mathf.Sqrt(Mathf.Pow(tile2.x - tile1.x, 2) + Mathf.Pow(tile2.y - tile1.y, 2));
        }

        //Rebuild edges
        private static LinkedList<PortalEdge> RebuildPath(Dictionary<int2, PortalEdge> Parent, Portal dest) {
            LinkedList<PortalEdge> res = new LinkedList<PortalEdge>();
            int2 current = dest.pos;
            PortalEdge e = null;

            while (Parent.TryGetValue(current, out e)) {
                res.AddFirst(e);
                current = e.start.pos;
            }

            return res;
        }
    }

}