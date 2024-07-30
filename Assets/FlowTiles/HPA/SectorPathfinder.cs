using FlowTiles.PortalGraphs;
using Priority_Queue;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles {
    public class SectorPathfinder {

        private static int2[] Offsets = new int2[] {
            new int2 (1, 0),new int2 (-1, 0),new int2 (0, 1),new int2 (0, -1),
        };

        public static int FindTravelCost(CostField costs, int2 start, int2 dest) {
            HashSet<int2> Visited = new HashSet<int2>();
            Dictionary<int2, int2> Parent = new Dictionary<int2, int2>();
            Dictionary<int2, int> gScore = new Dictionary<int2, int>();
            SimplePriorityQueue<int2, float> pq = new SimplePriorityQueue<int2, float>();

            gScore[start] = 0;
            pq.Enqueue(start, EuclidianDistance(start, dest));
            int2 current;

            while (pq.Count > 0) {
                current = pq.Dequeue();

                if (current.Equals(dest)) {
                    //Rebuild path and return it
                    return gScore[current];
                }

                Visited.Add(current);

                //Visit all neighbours through edges going out of node
                foreach (var offset in Offsets) {

                    // Find the neighbor cell
                    var next = current + offset;
                    if (next.x < 0 || next.y < 0 ||
                        next.x >= costs.size.x || next.y >= costs.size.y) {
                        continue;
                    }

                    //Check if we visited the outer end of the edge
                    if (Visited.Contains(next)) {
                        continue;
                    }

                    // Check if the cell is passable
                    var cost = costs.Costs[next.x, next.y];
                    if (cost == CostField.WALL) {
                        continue;
                    }

                    int temp_gCost = gScore[current] + cost;

                    //If new value is not better then do nothing
                    if (gScore.TryGetValue(next, out int prev_gCost) && temp_gCost >= prev_gCost)
                        continue;

                    //Otherwise store the new value and add the destination into the queue
                    Parent[next] = current;
                    gScore[next] = temp_gCost;

                    pq.Enqueue(next, temp_gCost + EuclidianDistance(next, dest));
                }
            }

            return -1;
        }

        private static float EuclidianDistance(int2 tile1, int2 tile2) {
            return Mathf.Sqrt(Mathf.Pow(tile2.x - tile1.x, 2) + Mathf.Pow(tile2.y - tile1.y, 2));
        }


    }

}