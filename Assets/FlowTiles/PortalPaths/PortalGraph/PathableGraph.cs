using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using FlowTiles.Utils;

namespace FlowTiles.PortalPaths {

    [BurstCompile]
    public struct PathableGraph {

        public const int NUM_TRAVEL_TYPES = 1;

        public CellRect Bounds;
        public SectorLayout Layout;
        private NativeArray<Sector> Sectors; // Top level container must be native

        /// <summary>
        /// Construct a graph from the map
        /// </summary>
        public PathableGraph(int2 sizeCells, int resolution) {
            Bounds = new CellRect(0, sizeCells - 1);
            Layout = new SectorLayout(sizeCells, resolution);
            Sectors = new NativeArray<Sector>(Layout.NumSectorsInLevel, Allocator.Persistent);
        }

        public bool SectorIsInitialised (int index) {
            return Sectors[index].IsCreated;
        }

        public Sector IndexToSector(int index) {
            var sector = Sectors[index];
            return sector;
        }

        public Sector CellToSector(int2 pos) {
            var sectorX = pos.x / Layout.Resolution;
            var sectorY = pos.y / Layout.Resolution;
            var sector = Sectors[sectorX + sectorY * Layout.SizeSectors.x];
            return sector;
        }

        public SectorMap IndexToSectorMap (int index, int travelType) {
            var sector = Sectors[index];
            var map = sector.Maps[travelType];
            return map;
        }

        public SectorMap CellToSectorMap (int2 pos, int travelType) {
            var sectorX = pos.x / Layout.Resolution;
            var sectorY = pos.y / Layout.Resolution;
            var sector = Sectors[sectorX + sectorY * Layout.SizeSectors.x];
            var map = sector.Maps[travelType];
            return map;
        }

        public void ReinitialiseSector(int index, PathableLevel level) {
            int version = 0;
            if (Sectors[index].IsCreated) {
                version = Sectors[index].Version + 1;
                Sectors[index].Dispose();
            }

            Sectors[index] = new Sector(index, version, Layout.GetSectorBounds(index), level);
            for (int travelType = 0; travelType < NUM_TRAVEL_TYPES; travelType++) {
                Sectors[index].Maps[travelType].Initialise(level);
            }
        }

        public void BuildSectorExits(int index) {
            var x = index % Layout.SizeSectors.x;
            var y = index / Layout.SizeSectors.x;
            if (x < Layout.SizeSectors.x - 1) {
                BuildExit(index, index + 1, true, 1);
            }
            if (x > 0) {
                BuildExit(index, index - 1, true, -1);
            }
            if (y < Layout.SizeSectors.y - 1) {
                BuildExit(index, index + Layout.SizeSectors.x, false, 1);
            }
            if (y > 0) {
                BuildExit(index, index - Layout.SizeSectors.x, false, -1);
            }
        }

        private void BuildExit(int index1, int index2, bool horizontal, int flip) {
            for (int t = 0; t < NUM_TRAVEL_TYPES; t++) {
                var costs1 = Sectors[index1].Maps[t].Costs;
                var costs2 = Sectors[index2].Maps[t].Costs;
                var bounds = costs1.Bounds;

                int i = 0;
                int lineSize = 0;

                var maps = Sectors[index1].Maps;
                var map = maps[t];
                var portals = map.Portals;
                if (horizontal) {
                    int x1 = (flip > 0) ? costs1.Bounds.MaxCell.x : costs1.Bounds.MinCell.x;
                    var x2 = (flip > 0) ? costs2.Bounds.MinCell.x : costs2.Bounds.MaxCell.x;
                    for (i = bounds.MinCell.y; i <= bounds.MaxCell.y; i++) {
                        if (costs1.IsOpenAt(new int2(x1, i)) &&
                            costs2.IsOpenAt(new int2(x2, i))) {
                            lineSize++;
                            continue;
                        }
                        portals.CreateExit(index2, horizontal, lineSize, i, flip);
                        lineSize = 0;
                    }
                }
                if (!horizontal) {
                    var y1 = (flip > 0) ? costs1.Bounds.MaxCell.y : costs1.Bounds.MinCell.y;
                    var y2 = (flip > 0) ? costs2.Bounds.MinCell.y : costs2.Bounds.MaxCell.y;
                    for (i = bounds.MinCell.x; i <= bounds.MaxCell.x; i++) {
                        if (costs1.IsOpenAt(new int2(i, y1)) &&
                            costs2.IsOpenAt(new int2(i, y2))) {
                            lineSize++;
                            continue;
                        }
                        portals.CreateExit(index2, horizontal, lineSize, i, flip);
                        lineSize = 0;
                    }
                }

                portals.CreateExit(index2, horizontal, lineSize, i, flip);

                map.Portals = portals;
                maps[t] = map;
            }
        }

        public void BuildSector(int index) {
            SectorPathfinder pathfinder = new SectorPathfinder(Layout.NumCellsInSector, Allocator.Temp);
            var sector = Sectors[index];
            for (int travelType = 0; travelType < NUM_TRAVEL_TYPES; travelType++) {
                var map = sector.Maps[travelType];
                map.Costs.CalculateColors();
                map.Portals.BuildInternalConnections(map.Costs, pathfinder);
                sector.Maps[travelType] = map;
            }
        }

    }

    public struct Sector {

        public readonly int Index;
        public readonly int Version;
        public readonly CellRect Bounds;
        public UnsafeArray<SectorMap> Maps;

        public Sector(int index, int version, CellRect boundaries, PathableLevel level) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            Maps = new UnsafeArray<SectorMap>(1, Allocator.Persistent);
            for (int i = 0; i < Maps.Length; i++) {
                var map = new SectorMap(Index, Bounds, i, version);
                map.Costs.Initialise(level);
                Maps[i] = map;
            }
        }

        public bool IsCreated => Maps.IsCreated;

        public void Dispose () {
            for (int i = 0; i < Maps.Length; i++) {
                Maps[i].Dispose();
            }
            Maps.Dispose();
        }

    }

    public struct SectorMap {

        public readonly int Index;
        public readonly CellRect Bounds;
        public readonly int Version;

        public CostMap Costs;
        public PortalMap Portals;

        public SectorMap(int index, CellRect boundaries, int movementType, int version) {
            Index = index;
            Bounds = boundaries;
            Version = version;

            Costs = new CostMap(index, boundaries, movementType);
            Portals = new PortalMap(index, boundaries);
        }

        public void Initialise(PathableLevel level) {
            Costs.Initialise(level);
        }

        public void Dispose() {
            Costs.Dispose();
            Portals.Dispose();
        }

        public int GetCellColor(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            return Costs.Colors[localCell.x, localCell.y];
        }

        public Portal GetRootPortal(int2 cell) {
            var localCell = cell - Bounds.MinCell;
            var color = Costs.Colors[localCell.x, localCell.y];
            return Portals.RootPortals[color - 1];
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