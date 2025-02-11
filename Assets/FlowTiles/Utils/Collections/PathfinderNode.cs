using System;
using Unity.Mathematics;

namespace FlowTiles.Utils {
    public struct PathfinderNode : IComparable<PathfinderNode> {

        public readonly int2 Position;
        public readonly float Cost;
        public readonly bool IsStartNode;

        public PathfinderNode(int2 position, float expectedCost, bool isStartNode = false) {
            Position = position;
            Cost = expectedCost;
            IsStartNode = isStartNode;
        }

        public int CompareTo(PathfinderNode other) {
            return (int)math.sign(Cost - other.Cost);
        }

    }

}