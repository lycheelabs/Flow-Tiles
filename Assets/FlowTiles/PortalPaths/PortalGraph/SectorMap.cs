using Unity.Mathematics;

namespace FlowTiles.PortalPaths {
    public struct SectorMap {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int Version;

        public CostMap Costs;
        public IslandMap Islands;
        public ColorMap Colors;
        public PortalMap Portals;

        public bool IsFullyBlocked => Colors.NumColors <= 0;

        public SectorMap(int index, CellRect boundaries, int travelType, int version) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            Costs = new CostMap(index, boundaries, travelType);
            Islands = new IslandMap(index, boundaries);
            Colors = new ColorMap(index, boundaries);
            Portals = new PortalMap(index, boundaries);
        }

        public void Initialise(PathableLevel level) {
            Costs.Initialise(level);
        }

        public void Dispose() {
            Costs.Dispose();
            Islands.Dispose();
            Colors.Dispose();
            Portals.Dispose();
        }

        public int GetCellIsland(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            return Islands.Cells[localCell.x, localCell.y];
        }

        public int GetCellColor(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            return Colors.Cells[localCell.x, localCell.y];
        }

        public Portal GetRootPortal(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            var island = Islands.Cells[localCell.x, localCell.y];
            return Portals.RootPortals[island - 1];
        }

        public bool TryGetExitPortal(int2 cell, out Portal portal) {
            if (!Portals.HasExitPortalAt(cell)) {
                portal = default;
                return false;
            }
            portal = Portals.GetExitPortalAt(cell);
            return true;
        }

        public Portal GetExitPortal(int2 cell) {
            return Portals.GetExitPortalAt(cell);
        }

    }

}