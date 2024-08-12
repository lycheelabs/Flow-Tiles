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
        public NativeField<byte> Costs;
        
        public readonly int NumTravelTypes;
        public readonly int NumTerrainTypes;
        public NativeArray<TerrainCosts> TerrainCosts;

        public NativeField<SectorFlags> SectorFlags;
        public NativeReference<bool> NeedsRebuilding;

        public PathableLevel(int width, int height, int resolution, int numTravelTypes = 1, int numTerrainTypes = 1) {
            Size = new int2(width, height);
            Layout = new SectorLayout(Size, resolution);
            Bounds = new CellRect(0, Size - 1);

            Obstacles = new NativeField<bool>(Size, Allocator.Persistent);
            Terrain = new NativeField<byte>(Size, Allocator.Persistent);
            Costs = new NativeField<byte>(Size, Allocator.Persistent);
            
            NumTravelTypes = numTravelTypes;
            NumTerrainTypes = numTerrainTypes;
            TerrainCosts = new NativeArray<TerrainCosts>(numTravelTypes, Allocator.Persistent);
            for (int i = 0; i < numTravelTypes; i++) {
                TerrainCosts[i] = new TerrainCosts(numTerrainTypes);
            }

            var initialise = new SectorFlags { NeedsRebuilding = true };
            SectorFlags = new NativeField<SectorFlags>(Layout.SizeSectors, Allocator.Persistent, initialise);
            NeedsRebuilding = new NativeReference<bool>(true, Allocator.Persistent);
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
            NeedsRebuilding.Value = true;
            UpdateRebuildFlags(x, y);
        }

        public void SetTerrain(int x, int y, byte type) {
            Terrain[x, y] = type;
            NeedsRebuilding.Value = true;
            UpdateRebuildFlags(x, y);
        }

        private void UpdateRebuildFlags(int x, int y) {
            var size = Layout.SizeSectors;
            var resolution = Layout.Resolution;
            var sectorX = x / resolution;
            var sectorY = y / resolution;
            var cellX = x % resolution;
            var cellY = y % resolution;

            var rebuild = new SectorFlags { NeedsRebuilding = true };
            SectorFlags[sectorX, sectorY] = rebuild;

            if (sectorX > 0 && cellX == 0) {
                SectorFlags[sectorX - 1, sectorY] = rebuild;
            }
            if (sectorY > 0 && cellY == 0) {
                SectorFlags[sectorX, sectorY - 1] = rebuild;
            }
            if (sectorX < size.x - 1 && cellX == resolution - 1) {
                SectorFlags[sectorX + 1, sectorY] = rebuild;
            }
            if (sectorY < size.y - 1 && cellY == resolution - 1) {
                SectorFlags[sectorX, sectorY + 1] = rebuild;
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
            var extraCost = Costs[x, y];
            return (byte)math.min(terrainCost + extraCost, WALL_COST - 1);
        }

    }

}