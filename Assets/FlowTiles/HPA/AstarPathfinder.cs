using FlowTiles.PortalGraphs;
using Priority_Queue;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles {

    public class AstarPathfinder {

        private static float EuclidianDistance(Portal node1, Portal node2) {
            return EuclidianDistance(node1.pos, node2.pos);
        }


        private static float EuclidianDistance(int2 tile1, int2 tile2) {
            return Mathf.Sqrt(Mathf.Pow(tile2.x - tile1.x, 2) + Mathf.Pow(tile2.y - tile1.y, 2));
        }

        public static LinkedList<PortalEdge> FindPath(Portal start, Portal dest, Boundaries boundaries = null) {
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
                if (current.pos.Equals(dest.pos))
                    //Rebuild path and return it
                    return RebuildPath(Parent, current);


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