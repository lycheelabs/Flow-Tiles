using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalPaths {

    public struct PortalPathNode {

        public static int4 FlowCacheKey (int2 cell, int2 direction, int travelType) {
            return new int4(cell, DirectionToIndex(direction), travelType);
        }

        private static int DirectionToIndex(int2 direction) => direction.x * 2 + direction.y;

        public static PortalPathNode NewDestNode(Portal cluster, int2 cell, int version) {
            return new PortalPathNode {
                Position = new SectorCell(cluster.Position.SectorIndex, cell),
                GoalBounds = new CellRect(cell, cell),
                Direction = 0,
                Color = cluster.Color,
                Version = version,
            };
        }

        // ----------------------------------------------------------------------

        public SectorCell Position;
        public CellRect GoalBounds;
        public int2 Direction;
        public int Color;
        public int Version;

        public int4 CacheKey (int travelType) => FlowCacheKey(Position.Cell, Direction, travelType);


    }

   
}