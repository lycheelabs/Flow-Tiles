using LycheeLabs.FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.PortalPaths {

    public struct SectorCosts {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int MovementType;

        public UnsafeField<byte> Cells;

        public SectorCosts(int index, CellRect boundaries, int movementType) {
            Index = index;
            Bounds = new CellRect();
            MovementType = movementType;

            Bounds = boundaries;
            Cells = new UnsafeField<byte>(Bounds.SizeCells, Allocator.Persistent, initialiseTo: 1);
        }

        public void Initialise(PathableGrid map) {
            CopyCosts(map, Bounds.MinCell);
        }

        public void Dispose() {
            if (Cells.IsCreated) {
                Cells.Dispose();
            }
        }

        public bool Contains(int2 pos) {
            return pos.x >= Bounds.MinCell.x &&
                pos.x <= Bounds.MaxCell.x &&
                pos.y >= Bounds.MinCell.y &&
                pos.y <= Bounds.MaxCell.y;
        }

        public bool IsOpenAt(int2 pos) {
            if (!Contains(pos)) return false;
            var localPos = pos - Bounds.MinCell;
            if (localPos.x < 0 || localPos.x >= Cells.Size.x || localPos.y < 0 || localPos.y >= Cells.Size.y) {
                return false;
            }
            return Cells[localPos.x, localPos.y] < PathableGrid.MAX_COST;
        }

        public byte GetCostAt(int2 pos) {
            if (!Contains(pos)) {
                return PathableGrid.MAX_COST;
            }
            var localPos = pos - Bounds.MinCell;
            if (localPos.x < 0 || localPos.x >= Cells.Size.x || localPos.y < 0 || localPos.y >= Cells.Size.y) {
                return PathableGrid.MAX_COST;
            }
            return Cells[localPos.x, localPos.y];
        }

        // --------------------------------------------------------------

        private void CopyCosts(PathableGrid map, int2 corner) {
            for (int x = 0; x < Cells.Size.x; x++) {
                for (var y = 0; y < Cells.Size.y; y++) {
                    Cells[x, y] = map.GetCostAt(corner.x + x, corner.y + y, MovementType);
                }
            }
        }

    }

}