using System;
using FlowTiles.PortalPaths;
using FlowTiles.Utils;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles {

    public struct TerrainCosts {
        public UnsafeArray<byte> Mapping;
        public TerrainCosts(int numTerrainTypes) {
            Mapping = new UnsafeArray<byte>(numTerrainTypes, Allocator.Persistent, initialiseTo: 1);
        } 
        public byte GetCost (int terrainType) {
            if (terrainType < 0 || terrainType >= Mapping.Length) return 0;
            return 1;
        }
        public void Dispose() {
            Mapping.Dispose();
        }
    }

    public struct PathableLevel {

        public const byte MAX_COST = 255;

        public readonly int2 Size;
        public readonly SectorLayout Layout;
        public readonly CellRect Bounds;

        public NativeField<bool> Blocked;
        public NativeField<byte> Terrain;
        public NativeField<byte> TerrainAdjustments;
        public NativeField<byte> Obstacles;

        public readonly int NumTravelTypes;
        public readonly int NumTerrainTypes;
        public NativeArray<TerrainCosts> TerrainCosts;

        public NativeReference<bool> IsInitialised;
        public NativeReference<bool> NeedsRebuilding;
        public NativeField<SectorFlags> RebuildFlags;

        public PathableLevel(int width, int height, int resolution, int numTravelTypes = 1, int numTerrainTypes = 1) {
            Size = new int2(width, height);
            Layout = new SectorLayout(Size, resolution);
            Bounds = new CellRect(0, Size - 1);

            Blocked = new NativeField<bool>(Size, Allocator.Persistent);
            Terrain = new NativeField<byte>(Size, Allocator.Persistent);
            TerrainAdjustments = new NativeField<byte>(Size, Allocator.Persistent);
            Obstacles = new NativeField<byte>(Size, Allocator.Persistent);
            
            NumTravelTypes = numTravelTypes;
            NumTerrainTypes = numTerrainTypes;
            TerrainCosts = new NativeArray<TerrainCosts>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < numTravelTypes; i++) {
                TerrainCosts[i] = new TerrainCosts(numTerrainTypes);
            }

            var initialise = new SectorFlags { NeedsRebuilding = true };
            IsInitialised = new NativeReference<bool>(false, Allocator.Persistent);
            NeedsRebuilding = new NativeReference<bool>(true, Allocator.Persistent);
            RebuildFlags = new NativeField<SectorFlags>(Layout.SizeSectors, Allocator.Persistent, initialise);
        }

        public void Dispose() {
            for (int i = 0; i < TerrainCosts.Length; i++) {
                TerrainCosts[i].Dispose();
            }

            Blocked.Dispose();
            Terrain.Dispose();
            Obstacles.Dispose();
            TerrainAdjustments.Dispose();

            TerrainCosts.Dispose();

            IsInitialised.Dispose();
            NeedsRebuilding.Dispose();
            RebuildFlags.Dispose();
        }

        public void SetTerrainCost(int travelType, int terrainType, byte newCost) {
            if (newCost <= 0 || newCost > MAX_COST) {
                throw new ArgumentException("Terrain costs must be in range 1-255");
            }
            if (travelType < 0 || travelType >= NumTravelTypes) {
                throw new ArgumentException("The provided movement type is not known");
            }
            if (terrainType < 0 || terrainType >= NumTerrainTypes) {
                throw new ArgumentException("The provided terrain type is not known");
            }

            var costSet = TerrainCosts[travelType];
            var costs = costSet.Mapping;
            costs[terrainType] = newCost;
            costSet.Mapping = costs;
            TerrainCosts[travelType] = costSet;
        }

        /// <summary>
        /// Blocks fully prevent pathfinding through this cell.
        /// </summary>
        public void SetBlocked (int x, int y, bool blocked = true) {
            Blocked[x, y] = blocked;
            UpdateRebuildFlags(new int2(x, y));
        }

        /// <summary>
        /// Terrain provides the base travel cost of this cell. 
        /// This cost can be different for different travel types.
        /// </summary>
        public void SetTerrain(int x, int y, byte type) {
            Terrain[x, y] = type;
            UpdateRebuildFlags(new int2(x, y));
        }

        /// <summary>
        /// Adds additional terrain cost to this cell (for all travel types)
        /// </summary>
        public void SetTerrainAdjustment(int x, int y, byte type) {
            TerrainAdjustments[x, y] = type;
            UpdateRebuildFlags(new int2(x, y));
        }

        /// <summary>
        /// Obstacles slow down pathfinding (additional to the base terrain cost).
        /// </summary>
        public void SetObstacle(int x, int y, byte cost) {
            Obstacles[x, y] = cost;
            UpdateRebuildFlags(new int2(x, y));
        }

        /// <summary>
        /// Stamps can efficiently set the obstacle costs of many cells at once.
        /// </summary>
        public void PlaceStamp (int cornerX, int cornerY, CostStamp stamp) {
            for (int offsetX = 0; offsetX < stamp.Size.x; offsetX++) {
                for (int offsetY = 0; offsetY < stamp.Size.y; offsetY++) {
                    var x = cornerX + offsetX;
                    var y = cornerY + offsetY;
                    if (x >= 0 && x < Size.x && y >= 0 && y < Size.y) {
                        var stampValue = stamp[offsetX, offsetY];
                        if (stampValue > 0) {
                            Obstacles[x, y] = stampValue;
                        }
                    }
                }
            }
            var corner = new int2(cornerX, cornerY);
            UpdateRebuildFlags(corner, stamp.Size);
        }

        /// <summary>
        /// Clears the obstacle costs of all cell within this stamp.
        /// </summary>
        public void ClearStamp(int cornerX, int cornerY, CostStamp stamp) {
            for (int offsetX = 0; offsetX < stamp.Size.x; offsetX++) {
                for (int offsetY = 0; offsetY < stamp.Size.y; offsetY++) {
                    var x = cornerX + offsetX;
                    var y = cornerY + offsetY;
                    if (x >= 0 && x < Size.x && y >= 0 && y < Size.y) {
                        var stampValue = stamp[offsetX, offsetY];
                        if (stampValue > 0) {
                            Obstacles[x, y] = 0;
                        }
                    }
                }
            }
            var corner = new int2(cornerX, cornerY);
            UpdateRebuildFlags(corner, stamp.Size);
        }

        private void UpdateRebuildFlags(int2 cell) {
            UpdateRebuildFlags(cell, 1);
        }

        private void UpdateRebuildFlags(int2 corner, int2 size) {
            if (!IsInitialised.Value) return; // Skip until first build of all sectors

            var sizeSectors = Layout.SizeSectors;
            var resolution = Layout.Resolution;
            var min = corner - 1;
            var max = corner + size;
            var minSector = min / resolution;
            var maxSector = max / resolution;

            for (int x = minSector.x; x <= maxSector.x; x++) {
                if (x < 0 || x >= sizeSectors.x) continue;
                for (int y = minSector.y; y <= maxSector.y; y++) {
                    if (y < 0 || y >= sizeSectors.y) continue;
                    RebuildFlags[x, y] = SectorFlags.Rebuild;
                    NeedsRebuilding.Value = true;
                }
            }
        }

        public byte GetCostAt (int x, int y, int travelType) {
            if (Blocked[x, y]) {
                return MAX_COST;
            }

            int terrainType = Terrain[x, y];
            travelType = math.clamp(travelType, 0, NumTravelTypes - 1);
            terrainType = math.clamp(terrainType, 0, NumTerrainTypes - 1);
            var terrainCost = TerrainCosts[travelType].Mapping[terrainType];

            var extraCost = TerrainAdjustments[x,y] + Obstacles[x, y];
            return (byte)math.min(terrainCost + extraCost, MAX_COST);
        }

    }

}