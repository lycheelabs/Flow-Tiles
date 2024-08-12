using UnityEngine;

namespace FlowTiles {

    public static class Constants {

        // These constants are used to provide default size of native collections.
        // Setting them smaller saves memory. Setting them too small will trigger re-sizing.
        public const int EXPECTED_MAX_COLORS = 8;
        public const int EXPECTED_MAX_EXITS = 16;
        public const int EXPECTED_MAX_EDGES = 16;
        public const int EXPECTED_MAX_PATH_LENGTH = 32;

    }

}