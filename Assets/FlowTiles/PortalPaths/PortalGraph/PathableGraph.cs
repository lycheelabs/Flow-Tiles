using System;
using Unity.Collections;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.PortalPaths {

    public struct PathableGraph {

        // Note: Top level data structures must be NativeContainers.
        // Nested data structures must be UnsafeContainers.

        public SectorLayout Layout;
        public int NumTravelTypes;

        public bool IsCreated;
        public NativeReference<int> GraphVersion;
        private NativeArray<GraphSector> Sectors;

        public int SectorSize => Layout.Resolution;
        public int SectorRounding => Layout.Rounding;
        public CellRect Bounds => Layout.LevelBounds;

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(PathableGrid grid) {
            Layout = grid.Layout;
            NumTravelTypes = grid.NumTravelTypes;

            IsCreated = true;
            GraphVersion = new NativeReference<int>(Allocator.Persistent);
            Sectors = new NativeArray<GraphSector>(Layout.NumSectorsInLevel, Allocator.Persistent);
        }

        public void Dispose() {
            if (!IsCreated) return;
            IsCreated = false;

            for (int i = 0; i < Sectors.Length; i++) {
                Sectors[i].Dispose();
            }
            if (Sectors.IsCreated) Sectors.Dispose();
            if (GraphVersion.IsCreated) GraphVersion.Dispose();
        }

        public bool SectorIsInBounds(int index) => (index >= 0 && index < Sectors.Length);

        public bool SectorIsInitialised(int index) {
            return SectorIsInBounds(index) && Sectors[index].IsCreated;
        }

        public int CellToIndex(int2 cell) {
            if (!Bounds.ContainsCell(cell))
                throw new IndexOutOfRangeException($"Cell {cell} is outside bounds {Bounds}");
            var sectorX = cell.x / Layout.Resolution;
            var sectorY = cell.y / Layout.Resolution;
            return sectorX + sectorY * Layout.SizeSectors.x;
        }

        public GraphSector IndexToSector(int index) {
            if (index < 0 || index >= Sectors.Length)
                throw new IndexOutOfRangeException($"Sector index {index} is out of bounds [0, {Sectors.Length})");
            return Sectors[index];
        }

        public GraphSector CellToSector(int2 pos) {
            return Sectors[CellToIndex(pos)];
        }

        public int CellToCost(int2 pos, int travelType) {
            var sector = CellToSectorMap(pos, travelType);
            var corner = sector.Bounds.MinCell;
            return sector.Costs.Cells[pos.x - corner.x, pos.y - corner.y];
        }

        public SectorData IndexToSectorMap (int index, int travelType) {
            if (index < 0 || index >= Sectors.Length)
                throw new IndexOutOfRangeException($"Sector index {index} is out of bounds [0, {Sectors.Length})");
            var sector = Sectors[index];
            var data = sector.GetData(travelType);
            return data;
        }

        public SectorData CellToSectorMap (int2 pos, int travelType) {
            if (!Bounds.ContainsCell(pos))
                throw new IndexOutOfRangeException($"Cell {pos} is outside bounds {Bounds}");
            var sectorX = pos.x / Layout.Resolution;
            var sectorY = pos.y / Layout.Resolution;
            var index = sectorX + sectorY * Layout.SizeSectors.x;
            if (index < 0 || index >= Sectors.Length)
                throw new IndexOutOfRangeException($"Calculated sector index {index} is out of bounds [0, {Sectors.Length})");
            var sector = Sectors[index];
            var data = sector.GetData(travelType);
            return data;
        }

        public GraphSector InstantiateSector(int index, PathableGrid level) {
            if (index < 0 || index >= Sectors.Length)
                throw new IndexOutOfRangeException($"Sector index {index} is out of bounds [0, {Sectors.Length})");
            int sectorVersion = Sectors[index].Version + 1;
            var sectorBounds = Layout.GetSectorBounds(index);
            return new GraphSector(index, sectorVersion, sectorBounds, ref level, NumTravelTypes);
        }

        public void StoreSector(int index, GraphSector newSector) {
            if (index < 0 || index >= Sectors.Length)
                throw new IndexOutOfRangeException($"Sector index {index} is out of bounds [0, {Sectors.Length})");
            Sectors[index].Dispose();
            Sectors[index] = newSector;
        }

        // ------------------------------------------

        public bool TryExtractContinent (float2 position, int travelType, out int continent) {
            continent = -1;
            var taskCell = new int2((int)math.floor(position.x), (int)math.floor(position.y));
            var taskSector = CellToIndex(taskCell);
            if (!SectorIsInitialised(taskSector)) {
                return false;
            }
            var map = IndexToSectorMap(taskSector, travelType);
            if (map.Portals.Roots.Length < 1) {
                return false;
            }
            var root = map.GetRoot(taskCell);
            continent = root.Continent;
            return true;

        }

        public int2 TryApplySectorRounding (int2 cell, int travelType) {
            var sector = CellToSectorMap(cell, travelType);
            var island = sector.GetCellIsland(cell);

            var rounded = ApplySectorRounding(cell);
            var roundedIsland = sector.GetCellIsland(rounded);

            if (island == roundedIsland) {
                return rounded;
            }
            return cell;
        }

        private int2 ApplySectorRounding (int2 cell) {
            var corner = (cell / SectorSize) * SectorSize;
            var offset = cell % SectorSize;
            var rounded = ((offset / SectorRounding) * SectorRounding) + SectorRounding / 2;
            rounded = math.min(rounded, SectorSize - 1);
            return corner + rounded;
        }

    }

}