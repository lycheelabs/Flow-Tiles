using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct SectorData {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int Version;

        public SectorCosts Costs;
        public SectorIslands Islands;
        public SectorPortals Portals;

        public SectorData (int index, CellRect boundaries, int travelType, int version) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            Costs = new SectorCosts(index, boundaries, travelType);
            Islands = new SectorIslands(index, boundaries);
            Portals = new SectorPortals(index, boundaries);
        }

        public void Initialise(PathableLevel level) {
            Costs.Initialise(level);
        }

        public void Dispose() {
            Costs.Dispose();
            Islands.Dispose();
            Portals.Dispose();
        }

        public int GetCellIsland(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            return Islands.Cells[localCell.x, localCell.y];
        }

        public SectorRoot GetRoot(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            var island = Islands.Cells[localCell.x, localCell.y];
            return Portals.Roots[island - 1];
        }

        public bool TryGetPortal(int2 cell, out Portal portal) {
            if (!Portals.HasExitPortalAt(cell)) {
                portal = default;
                return false;
            }
            portal = Portals.GetExitPortalAt(cell);
            return true;
        }

        public Portal GetPortal(int2 cell) {
            return Portals.GetExitPortalAt(cell);
        }

        public void SetPortal(int2 cell, Portal portal) {
            Portals.SetExitPortalAt(cell, portal);
        }
    }

}