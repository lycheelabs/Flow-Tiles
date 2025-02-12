using Unity.Mathematics;

namespace FlowTiles.ECS {

    public static class CacheKeys {

        public static int4 ToFlowKey(int2 start, int2 direction, int travelType) {
            var directionIndex = direction.x + direction.y * 3;
            return new int4(start, directionIndex, travelType);
        }

        public static int4 ToPathKey(int2 start, int2 dest, int2 levelSize, int travelType) {
            return new int4(CellToIndex(start, levelSize), CellToIndex(dest, levelSize), travelType, 0);
        }

        public static bool DestMatchesPathKey(int2 dest, int2 levelSize, int4 key) {
            return CellToIndex(dest, levelSize) == key.y;
        }

        // -------------------------------------------------------
        private static int CellToIndex(int2 cell, int2 levelSize) {
            return cell.x + cell.y * levelSize.x;
        }

    }

}