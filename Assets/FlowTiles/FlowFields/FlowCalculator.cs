using FlowTiles.ECS;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.FlowFields {

    public struct FlowCalculator {

        [ReadOnly] public int2 Size;
        [ReadOnly] public CostMap Costs;
        [ReadOnly] public ColorMap Colors;
        [ReadOnly] public CellRect GoalBounds;
        [ReadOnly] public int2 ExitDirection;
        public short Color;

        private UnsafeField<int2> BaseFlow;
        private NativeHashSet<int2> Visited;
        private NativeHashMap<int2, int> Distance;
        private NativePriorityQueue<PathfinderNode> Queue;
        private NativeArray<int2> Directions;

        public FlowCalculator(FindFlowsJob.Task task, Allocator allocator) 
            : this (task.Sector, task.GoalBounds, task.ExitDirection, allocator) {}

        public FlowCalculator(SectorMap sector, CellRect goalBounds, int2 exitDirection, Allocator allocator) {
            Size = sector.Bounds.SizeCells;
            Costs = sector.Costs;
            Colors = sector.Colors;
            GoalBounds = goalBounds;
            ExitDirection = exitDirection;
            Color = 0;

            var numCells = sector.Bounds.WidthCells * sector.Bounds.HeightCells;
            BaseFlow = new UnsafeField<int2>(numCells, allocator);
            Visited = new NativeHashSet<int2>(numCells, allocator);
            Distance = new NativeHashMap<int2, int>(numCells, allocator);
            Queue = new NativePriorityQueue<PathfinderNode>(numCells * 2, allocator);
            Directions = new NativeArray<int2>(4, allocator);

            Directions[0] = new int2(1, 0);
            Directions[1] = new int2(-1, 0);
            Directions[2] = new int2(0, 1);
            Directions[3] = new int2(0, -1);
        }

        public void Calculate(ref UnsafeField<float2> flow) {

            Visited.Clear();
            Distance.Clear();
            Queue.Clear();

            // Initialise the goal cells
            var sectorBounds = Costs.Bounds;
            var size = sectorBounds.SizeCells;

            var goalMin = GoalBounds.MinCell - sectorBounds.MinCell;
            var goalMax = GoalBounds.MaxCell - sectorBounds.MinCell;
            for (int x = goalMin.x; x <= goalMax.x; x++) {
                for (int y = goalMin.y; y <= goalMax.y; y++) {
                    var goal = new int2(x, y);

                    BaseFlow[goal.x, goal.y] = ExitDirection;
                    Distance[goal] = 0;
                    Queue.Enqueue(new PathfinderNode(goal, 0));
                }
            }
            Color = Colors.Cells[goalMin.x, goalMin.y];

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

                    // Calculate the new cost and compare against best cost
                    var cost = Costs.Cells[next.x, next.y];
                    int newCost = Distance[current] + cost;
                    if (Distance.TryGetValue(next, out int prev_gCost) && newCost >= prev_gCost) {
                        continue;
                    }

                    //Otherwise store the new value and add the destination into the queue
                    BaseFlow[next.x, next.y] = (current - next);
                    Distance[next] = newCost;

                    Queue.Enqueue(new PathfinderNode(next, newCost));
                }
            }

            // Combine flow directions to create diagonals
            for (int x = 0; x < Size.x; x++) {
                for (int y = 0; y < Size.y; y++) {
                    var cell1 = new int2(x, y);
                    var flow1 = BaseFlow[x, y];
                    flow[x, y] = flow1;

                    if (flow1.Equals(0)) continue;

                    var cell2 = cell1 + flow1;
                    if (IsIn(cell2) && Costs.Cells[cell2.x, cell2.y] < PathableLevel.MAX_COST) {
                        var flow2 = BaseFlow[cell2.x, cell2.y];

                        var cell3 = cell1 + flow2;
                        if (IsIn(cell3) && Costs.Cells[cell3.x, cell3.y] < PathableLevel.MAX_COST) {

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