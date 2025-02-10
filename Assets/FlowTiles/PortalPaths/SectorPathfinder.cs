using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalPaths {

    public struct SectorPathfinder {

        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, int2> Parent;
        private NativeHashMap<int2, int> GScore;
        private NativePriorityQueue<PathfinderNode> Queue;
        private NativeArray<int2> Directions;

        public SectorPathfinder(int sectorCells, Allocator allocator) {
            Visited = new NativeHashSet<int2>(sectorCells, allocator);
            Parent = new NativeHashMap<int2, int2>(sectorCells, allocator);
            GScore = new NativeHashMap<int2, int>(sectorCells, allocator);
            Queue = new NativePriorityQueue<PathfinderNode>(sectorCells * 2, allocator);
            
            Directions = new NativeArray<int2>(4, allocator);
            Directions[0] = new int2(1, 0);
            Directions[1] = new int2(-1, 0);
            Directions[2] = new int2(0, 1);
            Directions[3] = new int2(0, -1);
        }

        public void Dispose () {
            Visited.Dispose();
            Parent.Dispose();
            GScore.Dispose();
            Queue.Dispose();
            Directions.Dispose();
        }

        public int FindTravelCost(UnsafeField<byte> costs, int2 start, int2 dest) {
            Visited.Clear();
            Parent.Clear();
            GScore.Clear();
            Queue.Clear();

            GScore[start] = 0;
            Queue.Enqueue(new PathfinderNode(start, EuclidianDistance(start, dest)));
            int2 current;

            while (!Queue.IsEmpty) {
                current = Queue.Dequeue().Position;

                if (current.Equals(dest)) {
                    //Rebuild path and return it
                    return GScore[current];
                }

                Visited.Add(current);

                //Visit all neighbours through edges going out of node
                foreach (var offset in Directions) {

                    // Find the neighbor cell
                    var next = current + offset;
                    if (next.x < 0 || next.y < 0 ||
                        next.x >= costs.Size.x || next.y >= costs.Size.y) {
                        continue;
                    }

                    //Check if we visited the outer end of the edge
                    if (Visited.Contains(next)) {
                        continue;
                    }

                    // Check if the cell is passable
                    var cost = costs[next.x, next.y];
                    if (cost == PathableLevel.MAX_COST) {
                        continue;
                    }

                    int temp_gCost = GScore[current] + cost;

                    //If new value is not better then do nothing
                    if (GScore.TryGetValue(next, out int prev_gCost) && temp_gCost >= prev_gCost)
                        continue;

                    //Otherwise store the new value and add the destination into the queue
                    Parent[next] = current;
                    GScore[next] = temp_gCost;

                    Queue.Enqueue(new PathfinderNode(next, temp_gCost + EuclidianDistance(next, dest)));
                }
            }

            return 0;
        }

        private static float EuclidianDistance(int2 tile1, int2 tile2) {
            return Mathf.Sqrt(Mathf.Pow(tile2.x - tile1.x, 2) + Mathf.Pow(tile2.y - tile1.y, 2));
        }

    }

}