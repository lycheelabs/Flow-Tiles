using FlowTiles.Utils;
using Unity.Mathematics;

namespace FlowTiles.FlowFields {

    public struct FlowField {

        public int SectorIndex;
        public short Color;

        public int2 Size;
        public int2 Corner;
        public UnsafeField<float2> Directions;
        public UnsafeField<int> Distances;

        public int Version;

        public void Dispose() {
            Directions.Dispose();
            Distances.Dispose();
        }

        public float2 GetFlow (int x, int y) {
            x = math.clamp(x, 0, Size.x - 1);
            y = math.clamp(y, 0, Size.y - 1);
            return Directions[x, y];
        }

    }

}