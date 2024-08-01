using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct MapSector {

        public readonly CellRect Bounds;

        public CostField Costs;
        public ColorField Colors;

        public MapSector(CellRect boundaries) {
            Bounds = new CellRect();

            Bounds = boundaries;
            Costs = new CostField(Bounds.SizeCells);
            Colors = new ColorField(Bounds.SizeCells);
        }

        public MapSector Build(PathableMap map) {
            Costs.Initialise(map, Bounds.MinCell);
            Colors.Recolor(Costs);
            return this;
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool IsOpenAt(int2 pos) {
            if (Contains(pos)
                && Costs.GetCost(pos - Bounds.MinCell) != CostField.WALL) {
                return true;
            }
            return false;
        }

    }

}