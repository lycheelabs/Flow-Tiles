using FlowTiles.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalPaths {

    public struct PortalPathNode {

        public static PortalPathNode NewDestNode(Portal cluster, int2 cell, int version) {
            return new PortalPathNode {
                Position = new SectorCell(cluster.Center.SectorIndex, cell),
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

        public int4 FlowCacheKey (int travelType) => FlowCache.ToKey(Position.Cell, Direction, travelType);

    }

   
}