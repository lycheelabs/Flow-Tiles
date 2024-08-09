using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalPathNode {

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

        public int4 CacheKey (int travelType) => new int4(Position.Cell, DirectionToIndex(Direction), travelType);
        private int DirectionToIndex(int2 direction) => direction.x * 2 + direction.y;

    }

   
}