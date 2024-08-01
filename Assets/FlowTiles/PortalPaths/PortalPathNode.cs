using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct PortalPathNode {

        public SectorCell Position;
        public Boundaries GoalBounds;
        public int2 Direction;
        public int Color;

        public int4 CacheKey => new int4(Position.Cell, Direction);

        public static PortalPathNode NewDestNode(Portal cluster, int2 cell) {
            return new PortalPathNode {
                Position = new SectorCell(cluster.Position.SectorIndex, cell),
                GoalBounds = new Boundaries(cell, cell),
                Direction = 0,
                Color = cluster.Color,
            };
        }
    }

   
}