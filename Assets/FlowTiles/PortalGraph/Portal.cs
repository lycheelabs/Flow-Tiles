using System.Collections.Generic;

namespace FlowTiles {

    public class Portal {
        public GridTile pos;
        public List<PortalEdge> edges;
        public Portal child;

        public Portal(GridTile value) {
            pos = value;
            edges = new List<PortalEdge>();
        }
    }

}