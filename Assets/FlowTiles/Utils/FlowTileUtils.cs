using FlowTiles.ECS;
using FlowTiles.PortalPaths;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles {

    public static class FlowTileUtils {

        public static float2 GetFlowDirection(ref CachedFlowField flow, int2 flowCorner, float2 worldPosition) {
            var localPosition = worldPosition - (float2)flowCorner;
            var x = (int)math.round(localPosition.x);
            var y = (int)math.round(localPosition.y);
            return flow.FlowField.GetFlow(x, y);
        }

        public static float2 GetBestPathLineOfSightDirection(int2 pos, int currentNode, UnsafeList<PortalPathNode> nodes, ref PathableGraph graph, int travelType) {
            int2 dest = GetBestPathLineOfSight(pos, currentNode, nodes, ref graph, travelType);
            return math.normalizesafe(dest - pos);
        }

        public static int2 GetBestPathLineOfSight(int2 pos, int currentNode, UnsafeList<PortalPathNode> nodes, ref PathableGraph graph, int travelType) {
            int2 result = pos;

            for (int i = currentNode; i < nodes.Length; i++) {
                var newPathNode = nodes[i];
                var nodePos = newPathNode.Position.Cell;
                if (pos.Equals(nodePos)) {
                    continue;
                }
                if (HasLineOfSight(pos, nodePos, ref graph, travelType, precise: true)) {
                    result = nodePos;
                    continue;
                }
                break;
            }

            return result;
        }

        // LOS algorithm adapted from ChatGPT suggested code.
        public static bool HasLineOfSight(int2 start, int2 end, ref PathableGraph graph, int travelType, bool precise) {
            int dx = math.abs(end.x - start.x);
            int dy = math.abs(end.y - start.y);
            int sx = start.x < end.x ? 1 : -1;
            int sy = start.y < end.y ? 1 : -1;
            int err = dx - dy;
            bool isHorizontal = dx > dy;

            int2 current = start;
            var startCost = graph.CellToCost(current, travelType);

            while (true) {
                var cost = graph.CellToCost(current, travelType);
                if (cost != startCost) {
                    return false;
                }
                if (precise) {
                    if (CheckThickCollisions(current, start, end, isHorizontal, startCost, ref graph, travelType)) {
                        return false;
                    }
                }
                if (current.x == end.x && current.y == end.y) {
                    return true;
                }

                int e2 = 2 * err;
                if (e2 > -dy) {
                    err -= dy;
                    current.x += sx;
                }
                if (e2 < dx) {
                    err += dx;
                    current.y += sy;
                }
            }
        }

        // Check surrounding cells (to account for thickness)
        private static bool CheckThickCollisions(int2 center, float2 lineStart, float2 lineEnd, bool isHorizontal, int startCost, ref PathableGraph graph, int travelType) {
            var size = graph.Bounds.SizeCells;

            if (isHorizontal) {
                if (center.y > 0 && CheckThickCollisionCell(new int2(center.x, center.y - 1),
                    lineStart, lineEnd, isHorizontal, startCost, ref graph, travelType)) {
                    return true;
                }
                if (center.y < size.y - 1 && CheckThickCollisionCell(new int2(center.x, center.y + 1),
                    lineStart, lineEnd, isHorizontal, startCost, ref graph, travelType)) {
                    return true;
                }
            } else {
                if (center.x > 0 && CheckThickCollisionCell(new int2(center.x - 1, center.y),
                    lineStart, lineEnd, isHorizontal, startCost, ref graph, travelType)) {
                    return true;
                }
                if (center.x < size.x - 1 && CheckThickCollisionCell(new int2(center.x + 1, center.y),
                    lineStart, lineEnd, isHorizontal, startCost, ref graph, travelType)) {
                    return true;
                }
            }
            return false;
        }

        private static bool CheckThickCollisionCell(int2 cell, float2 lineStart, float2 lineEnd, bool isHorizontal, int startCost, ref PathableGraph graph, int travelType) {
            if (IntersectsCell(cell, lineStart, lineEnd)) {
                var cost = graph.CellToCost(cell, travelType);
                if (cost != startCost) {
                    return true;
                }
            }
            return false;
        }

        // Checks if a float line intersects a given grid cell
        private static bool IntersectsCell(int2 cell, float2 start, float2 end) {
            float2 min = new float2(cell.x, cell.y);
            float2 max = min + 1; // Tile bounds

            // Check if the line segment intersects the tile
            return LineIntersectsAABB(start, end, min, max);
        }

        // Line-AABB intersection test (Axis-Aligned Bounding Box)
        private static bool LineIntersectsAABB(float2 p1, float2 p2, float2 min, float2 max) {
            float2 d = p2 - p1;
            float tMin = 0, tMax = 1;

            for (int i = 0; i < 2; i++) // X and Y axes
            {
                if (math.abs(d[i]) < 1e-6) // Avoid division by zero
                {
                    if (p1[i] < min[i] || p1[i] > max[i]) return false;
                }
                else {
                    float t1 = (min[i] - p1[i]) / d[i];
                    float t2 = (max[i] - p1[i]) / d[i];

                    if (t1 > t2) (t1, t2) = (t2, t1); // Swap if needed

                    tMin = math.max(tMin, t1);
                    tMax = math.min(tMax, t2);

                    if (tMin > tMax) return false;
                }
            }

            return true;
        }

    }

}