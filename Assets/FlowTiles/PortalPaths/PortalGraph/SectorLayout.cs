using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.PortalGraphs {
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

        public int TileToIndex(int2 tile) {
            return SectorToIndex(TileToSector(tile));
        }

        public int TileToIndex(int tileX, int tileY) {
            return SectorToIndex(TileToSector(tileX, tileY));
        }

        public int2 TileToSector(int2 tile) {
            return new int2(tile.x / Resolution, tile.y / Resolution);
        }

        public int2 TileToSector (int tileX, int tileY) {
            return new int2(tileX / Resolution, tileY / Resolution);
        }

        public int SectorToIndex(int2 sector) {
            return sector.x + SizeSectors.x * sector.y;
        }

        public int SectorToIndex (int sectorX, int sectorY) {
            return sectorX + SizeSectors.x * sectorY;
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