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
    }

    public struct PathableLevel {

        public const byte WALL_COST = 255;

        public readonly int2 Size;
        public readonly SectorLayout Layout;
        public readonly CellRect Bounds;

        public NativeField<bool> Obstacles;
        public NativeField<byte> Terrain;
        public NativeField<byte> Stamps;
        
        public readonly int NumTravelTypes;
        public readonly int NumTerrainTypes;
        public NativeArray<TerrainCosts> TerrainCosts;

        public NativeReference<bool> IsInitialised;
        public NativeReference<bool> NeedsRebuilding;
        public NativeField<SectorFlags> SectorFlags;

        public PathableLevel(int width, int height, int resolution, int numTravelTypes = 1, int numTerrainTypes = 1) {
            Size = new int2(width, height);
            Layout = new SectorLayout(Size, resolution);
            Bounds = new CellRect(0, Size - 1);

            Obstacles = new NativeField<bool>(Size, Allocator.Persistent);
            Terrain = new NativeField<byte>(Size, Allocator.Persistent);
            Stamps = new NativeField<byte>(Size, Allocator.Persistent);
            
            NumTravelTypes = numTravelTypes;
            NumTerrainTypes = numTerrainTypes;
            TerrainCosts = new NativeArray<TerrainCosts>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < numTravelTypes; i++) {
                TerrainCosts[i] = new TerrainCosts(numTerrainTypes);
            }

            var initialise = new SectorFlags { NeedsRebuilding = true };
            IsInitialised = new NativeReference<bool>(false, Allocator.Persistent);
            NeedsRebuilding = new NativeReference<bool>(true, Allocator.Persistent);
            SectorFlags = new NativeField<SectorFlags>(Layout.SizeSectors, Allocator.Persistent, initialise);
        }

        public void SetTerrainCost(int travelType, int terrainType, byte newCost) {
            if (newCost <= 0 || newCost > WALL_COST) {
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

        public void SetObstacle (int x, int y, bool obstacle = true) {
            Obstacles[x, y] = obstacle;
            UpdateRebuildFlags(new int2(x, y), 1);
        }

        public void SetTerrain(int x, int y, byte type) {
            Terrain[x, y] = type;
            UpdateRebuildFlags(new int2(x, y), 1);
        }

        public void PlaceStamp (int cornerX, int cornerY, CostStamp stamp) {
            for (int offsetX = 0; offsetX < stamp.Size.x; offsetX++) {
                for (int offsetY = 0; offsetY < stamp.Size.y; offsetY++) {
                    var x = cornerX + offsetX;
                    var y = cornerY + offsetY;
                    if (x >= 0 && x < Size.x && y >= 0 && y < Size.y) {
                        var stampValue = stamp[offsetX, offsetY];
                        if (stampValue > 0) {
                            Stamps[x, y] = stampValue;
                        }
                    }
                }
            }
            var corner = new int2(cornerX, cornerX);
            UpdateRebuildFlags(corner, stamp.Size);
        }

        public void ClearStamp(int cornerX, int cornerY, CostStamp stamp) {
            for (int offsetX = 0; offsetX < stamp.Size.x; offsetX++) {
                for (int offsetY = 0; offsetY < stamp.Size.y; offsetY++) {
                    var x = cornerX + offsetX;
                    var y = cornerY + offsetY;
                    if (x >= 0 && x < Size.x && y >= 0 && y < Size.y) {
                        var stampValue = stamp[offsetX, offsetY];
                        if (stampValue > 0) {
                            Stamps[x, y] = 0;
                        }
                    }
                }
            }
            var corner = new int2(cornerX, cornerX);
            UpdateRebuildFlags(corner, stamp.Size);
        }

        private void UpdateRebuildFlags(int2 corner, int2 size) {
            if (!IsInitialised.Value) return; // Skip until first build of all sectors

            var sizeSectors = Layout.SizeSectors;
            var resolution = Layout.Resolution;
            var min = corner - 1;
            var max = corner + size;
            var minSector = min / resolution;
            var maxSector = max / resolution;

            var rebuild = new SectorFlags { NeedsRebuilding = true };
            for (int x = minSector.x; x <= maxSector.x; x++) {
                if (x < 0 || x >= sizeSectors.x) continue;
                for (int y = minSector.y; y <= maxSector.y; y++) {
                    if (y < 0 || y >= sizeSectors.y) continue;
                    SectorFlags[x, y] = rebuild;
                    NeedsRebuilding.Value = true;
                }
            }
        }

        public byte GetCostAt (int x, int y, int travelType) {
            var obstacle = Obstacles[x, y];
            if (obstacle) {
                return WALL_COST;
            }

            int terrainType = Terrain[x, y];
            travelType = math.clamp(travelType, 0, NumTravelTypes - 1);
            terrainType = math.clamp(terrainType, 0, NumTerrainTypes - 1);

            var terrainCost = TerrainCosts[travelType].Mapping[terrainType];
            var extraCost = Stamps[x, y];
            return (byte)math.min(terrainCost + extraCost, WALL_COST);
        }

    }

}