using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalPaths {
    public struct SectorLayout {

        public readonly int2 SizeCells;
        public readonly int2 SizeSectors;
        public readonly int Resolution;

        public SectorLayout (int2 sizeCells, int resolution) {
            SizeCells = sizeCells;
            Resolution = resolution;

            var sectorsW = Mathf.CeilToInt((float)sizeCells.x / resolution);
            var sectorsH = Mathf.CeilToInt((float)sizeCells.y / resolution);
            SizeSectors = new int2(sectorsW, sectorsH);
        }

        public int NumSectorsInLevel => SizeSectors.x * SizeSectors.y;
        public int NumCellsInSector => Resolution * Resolution;

        public int IndexOfCell (int2 cell) {
            return cell.x + SizeCells.x * cell.y;
        }

        public int IndexOfCell(int cellX, int cellY) {
            return cellX + SizeCells.x * cellY;
        }

        public int IndexOfSector(int2 sector) {
            return sector.x + SizeSectors.x * sector.y;
        }

        public int IndexOfSector(int sectorX, int sectorY) {
            return sectorX + SizeSectors.x * sectorY;
        }

        public int2 CellToSector(int2 cell) {
            return new int2(cell.x / Resolution, cell.y / Resolution);
        }

        public int2 CellToSector (int cellX, int cellY) {
            return new int2(cellX / Resolution, cellY / Resolution);
        }

        public int CellToSectorIndex(int2 cell) {
            return IndexOfSector(CellToSector(cell));
        }

        public int CellToSectorIndex(int cellX, int cellY) {
            return IndexOfSector(CellToSector(cellX, cellY));
        }

        public int2 GetMinCorner(int index) {
            var x = index % SizeSectors.x;
            var y = index / SizeSectors.y;
            var min = new int2(x * Resolution, y * Resolution);
            return min;
        }

        public CellRect GetSectorBounds(int index) {
            var x = index % SizeSectors.x;
            var y = index / SizeSectors.y;
            var min = new int2(x * Resolution, y * Resolution);
            var max = new int2(
                Mathf.Min(min.x + Resolution - 1, SizeCells.x - 1),
                Mathf.Min(min.y + Resolution - 1, SizeCells.y - 1));
            return new CellRect { MinCell = min, MaxCell = max };

        }

        public CellRect GetSectorBounds (int x, int y) {
            var min = new int2(x * Resolution, y * Resolution);
            var max = new int2(
                Mathf.Min(min.x + Resolution - 1, SizeCells.x - 1),
                Mathf.Min(min.y + Resolution - 1, SizeCells.y - 1));
            return new CellRect { MinCell = min, MaxCell = max };

        }

    }

}