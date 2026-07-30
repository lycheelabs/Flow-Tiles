using UnityEngine;

namespace LycheeLabs.FlowTiles {

    public static class PathfindingConstants {

        // These constants are used to provide default sizes of native collections.
        // Setting them smaller saves memory. Setting them too small will trigger re-sizing, which is slow.      
        public const int EXPECTED_MAX_ISLANDS = 8;
        public const int EXPECTED_MAX_EXITS = 16;
        public const int EXPECTED_MAX_EDGES = 16;
        public const int EXPECTED_MAX_PATH_LENGTH = 32;
        public const int EXPECTED_MAX_SEARCHED_NODES = 200;
        public const int EXPECTED_SECTORS_IN_MAP = 50;

        // These constants help split work over multiple frames.
        public const int EXPECTED_PC_CORES = 8;
        public const int MAX_REBUILDS_PER_FRAME = 1 * EXPECTED_PC_CORES;
        public const int MAX_PATHFINDS_PER_FRAME = 8 * EXPECTED_PC_CORES;
        public const int MAX_FLOWFIELDS_PER_FRAME = 2 * EXPECTED_PC_CORES;
        public const int MAX_SIGHTLINES_PER_FRAME = 16 * EXPECTED_PC_CORES;

        // This constant limits the number of paths that will be cached.
        // Older paths will be disposed when the limit is reached.
        public const int MAX_CACHED_PATHS = 5000;

        // This constant limits how many nodes are tested in a line-of-sight check.
        public const int MAX_LINE_OF_SIGHT_LOOKAHEAD = 5;

    }

}