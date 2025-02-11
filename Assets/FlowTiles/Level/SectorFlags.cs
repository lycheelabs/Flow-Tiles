using UnityEngine;

namespace FlowTiles {

    public struct SectorFlags {

        public static readonly SectorFlags Rebuild = new SectorFlags { NeedsRebuilding = true };

        public bool NeedsRebuilding;
        public bool IsReinitialised;
    }

}