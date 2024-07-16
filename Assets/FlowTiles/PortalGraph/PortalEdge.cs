
using System.Collections.Generic;

namespace FlowTiles {

    public class PortalEdge {
        public Portal start;
        public Portal end;
        public PortalEdgeType type;
        public float weight;

        public LinkedList<PortalEdge> UnderlyingPath;
    }


    public enum PortalEdgeType {
        INTRA,
        INTER
    }

}