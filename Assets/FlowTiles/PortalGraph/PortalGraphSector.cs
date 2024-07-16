using System.Collections.Generic;

namespace FlowTiles {

    /// <summary>
    /// Domain-independent, rectangular clusters
    /// </summary>
    public class PortalGraphSector {

        //Boundaries of the cluster (with respect to the original map)
        public Boundaries Boundaries;
        public Dictionary<GridTile, Portal> Portals;

        public int Width;
        public int Height;

        public PortalGraphSector() {
            Boundaries = new Boundaries();
            Portals = new Dictionary<GridTile, Portal>();
        }

        //Check if this cluster contains the other cluster (by looking at boundaries)
        public bool Contains(Cluster other) {
            return other.Boundaries.Min.x >= Boundaries.Min.x &&
                    other.Boundaries.Min.y >= Boundaries.Min.y &&
                    other.Boundaries.Max.x <= Boundaries.Max.x &&
                    other.Boundaries.Max.y <= Boundaries.Max.y;
        }

        public bool Contains(GridTile pos) {
            return pos.x >= Boundaries.Min.x &&
                pos.x <= Boundaries.Max.x &&
                pos.y >= Boundaries.Min.y &&
                pos.y <= Boundaries.Max.y;
        }

    }

}