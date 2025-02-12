namespace FlowTiles.ECS {
    public struct CachedSightline {

        public bool IsPending;
        public bool HasBeenQueued;

        public bool WasFound;
        public int GraphVersionAtSearch;

    }

}