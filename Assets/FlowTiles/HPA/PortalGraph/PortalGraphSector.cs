using System.Collections.Generic;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    /// <summary>
    /// Domain-independent, rectangular clusters
    /// </summary>
    public class PortalGraphSector {

        //Boundaries of the cluster (with respect to the original map)
        public Boundaries Boundaries;
        public Dictionary<int2, Portal> Portals;

        public int Width;
        public int Height;

        public PortalGraphSector() {
            Boundaries = new Boundaries();
            Portals = new Dictionary<int2, Portal>();
        }

        public bool Contains(int2 pos) {
            return pos.x >= Boundaries.Min.x &&
                pos.x <= Boundaries.Max.x &&
                pos.y >= Boundaries.Min.y &&
                pos.y <= Boundaries.Max.y;
        }

    }

}