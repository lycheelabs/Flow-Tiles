using System;
using LycheeLabs.FlowTiles.ECS;
using LycheeLabs.FlowTiles.PortalPaths;
using LycheeLabs.FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.FlowFields {

    public struct FlowCalculator {

        [ReadOnly] public int2 Size;
        [ReadOnly] public SectorCosts Costs;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public int2 ExitDirection;

        private UnsafeField<int2> BaseFlow;
        private NativeHashSet<int2> Visited;
        private NativePriorityQueue<PathfinderNode> Queue;
        private NativeArray<int2> Directions;

        public FlowCalculator(FindFlowsJob.Task task, Allocator allocator) 
            : this (task.Sector, task.GoalBounds, task.ExitDirection, allocator) {}

        public FlowCalculator(SectorData sector, CellRect goalBounds, int2 exitDirection, Allocator allocator)
            : this (sector.Costs, sector.Bounds, goalBounds, exitDirection, allocator) {}

        public FlowCalculator(SectorCosts sectorCosts, CellRect sectorBounds, CellRect goalBounds, int2 exitDirection, Allocator allocator) {
            Size = sectorBounds.SizeCells;
            Costs = sectorCosts;
            GoalBounds = goalBounds;
            ExitDirection = exitDirection;

            var numCells = sectorBounds.WidthCells * sectorBounds.HeightCells;
            BaseFlow = new UnsafeField<int2>(sectorBounds.SizeCells, allocator);
            Visited = new NativeHashSet<int2>(numCells, allocator);
            Queue = new NativePriorityQueue<PathfinderNode>(numCells * 2, allocator);
            Directions = new NativeArray<int2>(4, allocator);

            Directions[0] = new int2(1, 0);
            Directions[1] = new int2(-1, 0);
            Directions[2] = new int2(0, 1);
            Directions[3] = new int2(0, -1);
        }

        public void Dispose () {
            BaseFlow.Dispose();
            Visited.Dispose();
            Queue.Dispose();
            Directions.Dispose();
        }

        public void Calculate(ref UnsafeField<float2> flow, ref UnsafeField<int> distance, bool allowDiagonals) {

            Visited.Clear();
            Queue.Clear();

            // Initialise the goal cells
            var sectorBounds = Costs.Bounds;
            var goalMin = GoalBounds.MinCell - sectorBounds.MinCell;
            var goalMax = GoalBounds.MaxCell - sectorBounds.MinCell;
            bool seededAnyGoal = false;
            for (int x = goalMin.x; x <= goalMax.x; x++) {
                for (int y = goalMin.y; y <= goalMax.y; y++) {
                    var goal = new int2(x, y);

                    if (!BaseFlow.IsValidIndex(goal.x, goal.y) ||
                        !flow.IsValidIndex(goal.x, goal.y) ||
                        !distance.IsValidIndex(goal.x, goal.y)) {
                        continue;
                    }

                    BaseFlow[goal.x, goal.y] = ExitDirection;
                    distance[goal.x, goal.y] = 0;
                    Visited.Add(goal);
                    Queue.Enqueue(new PathfinderNode(goal, 0));
                    seededAnyGoal = true;
                }
            }

            if (!seededAnyGoal) {
                throw new InvalidOperationException($"Goal bounds {GoalBounds} do not overlap sector bounds {sectorBounds}");
            }

            // Iterate over the cells once in least-cost order
            int2 current;
            while (!Queue.IsEmpty) {
                current = Queue.Dequeue().Position;
                Visited.Add(current);

                //Visit all neighbours through edges going out of node
                foreach (var offset in Directions) {

                    // Find the neighbor cell
                    var next = current + offset;
                    if (!IsIn(next) || Visited.Contains(next)) {
                        continue;
                    }

                    // Calculate the new distance and compare against best distance
                    var cost = Costs.Cells[next.x, next.y];
                    int newDistance = distance[current.x, current.y] + cost;
                    var oldDistance = distance[next.x, next.y];
                    if (oldDistance > 0 && newDistance >= oldDistance) {
                        continue;
                    }

                    //Otherwise store the new value and add the destination into the queue
                    BaseFlow[next.x, next.y] = (current - next);
                    distance[next.x, next.y] = newDistance;

                    Queue.Enqueue(new PathfinderNode(next, newDistance));
                }
            }

            // Combine flow directions to create diagonals
            for (int x = 0; x < Size.x; x++) {
                for (int y = 0; y < Size.y; y++) {
                    var cell1 = new int2(x, y);
                    var flow1 = BaseFlow[x, y];
                    flow[x, y] = flow1;

                    if (flow1.Equals(0) || !allowDiagonals) continue;

                    var cell2 = cell1 + flow1;
                    if (IsIn(cell2) && Costs.Cells[cell2.x, cell2.y] < PathableGrid.MAX_COST) {
                        var flow2 = BaseFlow[cell2.x, cell2.y];

                        var cell3 = cell1 + flow2;
                        if (IsIn(cell3) && Costs.Cells[cell3.x, cell3.y] < PathableGrid.MAX_COST) {

                            var combinedFlow = math.normalize(flow1 + flow2);
                            flow[x, y] = combinedFlow;
                        }
                    }
                }
            }
        }

        private bool IsIn(int2 pos) {
            return 0 <= pos.x && pos.x < Size.x && 0 <= pos.y && pos.y < Size.y;
        }

    }

}