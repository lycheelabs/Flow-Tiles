using System.Collections.Generic;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public class Portal {
        public int2 pos;
        public List<PortalEdge> edges;
        public Portal child;

        public Portal(int2 value) {
            pos = value;
            edges = new List<PortalEdge>();
        }
    }

}