using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {

    public struct SectorFlags {
        public bool NeedsRebuilding;
    }

    public struct PathableLevel {

        public const byte WALL_COST = 255;

        public readonly int2 Size;
        public readonly SectorLayout Layout;
        public readonly CellRect Bounds;

        public NativeField<bool> Obstacles;
        public NativeField<byte> BaseCosts;
        public NativeField<byte> ModifiedCosts;
        public NativeField<SectorFlags> SectorFlags;
        public bool NeedsRebuilding;

        public PathableLevel(int width, int height, int resolution) {
            Size = new int2(width, height);
            Layout = new SectorLayout(Size, resolution);
            Bounds = new CellRect(0, Size - 1);

            Obstacles = new NativeField<bool>(Size, Allocator.Persistent);
            BaseCosts = new NativeField<byte>(Size, Allocator.Persistent, initialiseTo: 1);
            ModifiedCosts = new NativeField<byte>(Size, Allocator.Persistent);

            var initialise = new SectorFlags { NeedsRebuilding = true };
            SectorFlags = new NativeField<SectorFlags>(Layout.SizeSectors, Allocator.Persistent, initialise);
            NeedsRebuilding = true;
        }

        public void SetObstacle (int x, int y, bool obstacle = true) {
            Obstacles[x, y] = obstacle;
            var sectorX = x / Layout.Resolution;
            var sectorY = y / Layout.Resolution;
            SectorFlags[sectorX, sectorY] = new SectorFlags { NeedsRebuilding = true };
            NeedsRebuilding = true;
        }

        public byte GetCostAt (int x, int y) {
            var obstacle = Obstacles[x, y];
            if (obstacle) {
                return WALL_COST;
            }

            var modifiedCost = ModifiedCosts[x, y];
            if (modifiedCost > 0) {
                return modifiedCost;
            }

            return BaseCosts[x, y];
        }

    }

}