using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalPaths {

    public struct CostMap {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int MovementType;

        public UnsafeField<byte> Cells;

        public CostMap(int index, CellRect boundaries, int movementType) {
            Index = index;
            Bounds = new CellRect();
            MovementType = movementType;

            Bounds = boundaries;
            Cells = new UnsafeField<byte>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 1);
        }

        public void Initialise(PathableLevel map) {
            CopyCosts(map, Bounds.MinCell);
        }

        public void Dispose() {
            Cells.Dispose();
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool IsOpenAt(int2 pos) {
            var localPos = pos - Bounds.MinCell;
            return Contains(pos)
                && Cells[localPos.x, localPos.y] < PathableLevel.MAX_COST;
        }

        public byte GetCostAt(int2 pos) {
            var localPos = pos - Bounds.MinCell;
            return Cells[localPos.x, localPos.y];
        }

        // --------------------------------------------------------------

        private void CopyCosts(PathableLevel map, int2 corner) {
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    Cells[x, y] = map.GetCostAt(corner.x + x, corner.y + y, MovementType);
                }
            }
        }

    }

}