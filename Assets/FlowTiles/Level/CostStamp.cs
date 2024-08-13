using FlowTiles.Utils;
using Unity.Mathematics;

namespace FlowTiles {

    public struct CostStamp {

        public int Width => Values.Size.x;
        public int Height => Values.Size.y;
        public int2 Size => Values.Size;

        private readonly NativeField<byte> Values;
    
        public CostStamp(NativeField<byte> values) {
            Values = values;
        }

        public CostStamp(byte[,] values) {
            var size = new int2(values.GetLength(0), values.GetLength(1));
            Values = new NativeField<byte>(size, Unity.Collections.Allocator.Persistent); 
            for (int x = 0; x < size.x; x++) {
                for (int y = 0; y < size.y; y++) {
                    Values[x, y] = values[x, y];
                }
            }
        }

        public byte this[int x, int y] {
            get => Values[x + y * Size.x];
        }

    }

}