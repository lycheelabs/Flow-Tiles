using LycheeLabs.FlowTiles.Utils;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.FlowFields {

    public struct FlowField {

        public int SectorIndex;
        public int IslandIndex;

        public int2 Size;
        public int2 Corner;
        public UnsafeField<float2> Directions;
        public UnsafeField<int> Distances;

        public int Version;

        public bool IsCreated => Directions.IsCreated || Distances.IsCreated;

        public void Dispose() {
            if (Directions.IsCreated) {
                Directions.Dispose();
            }
            if (Distances.IsCreated) {
                Distances.Dispose();
            }
        }

        public float2 GetFlow (int x, int y) {
            x = math.clamp(x, 0, Size.x - 1);
            y = math.clamp(y, 0, Size.y - 1);
            return Directions[x, y];
        }

    }

}