using UnityEngine;

namespace FlowTiles {

    public static class Constants {

        // These constants are used to provide default sizes of native collections.
        // Setting them smaller saves memory. Setting them too small will trigger re-sizing, which is slow.      
        public const int EXPECTED_MAX_COLORS = 8;
        public const int EXPECTED_MAX_EXITS = 16;
        public const int EXPECTED_MAX_EDGES = 16;
        public const int EXPECTED_MAX_PATH_LENGTH = 16;

        // This constant limits the number of paths that will be cached.
        // Older paths will be disposed when the limit is reached.
        // If this isn't set large enough, no agents will get paths as they fight over slots.
        public const int MAX_CACHED_PATHS = 5000;

    }

}