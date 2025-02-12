using FlowTiles.ECS;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct PortalPathNode {

        public static PortalPathNode NewDestNode(SectorCell dest, int version) {
            return new PortalPathNode {
                Position = dest,
                GoalBounds = new CellRect(dest.Cell),
                Direction = 0,
                Version = version,
            };
        }

        // ----------------------------------------------------------------------

        public SectorCell Position;
        public CellRect GoalBounds;
        public int2 Direction;
        public int Version;

        public int4 FlowCacheKey (int travelType) => CacheKeys.ToFlowKey(Position.Cell, Direction, travelType);

    }

   
}