using UnityEngine;

namespace LycheeLabs.FlowTiles {

    public struct SectorFlags {

        public static readonly SectorFlags Rebuild = new SectorFlags { NeedsRebuilding = true };

        public bool NeedsRebuilding;

    }

}