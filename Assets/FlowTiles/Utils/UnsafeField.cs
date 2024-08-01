using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowTiles.Utils {

    public struct UnsafeField<T> where T : unmanaged {

        public readonly int2 Size;
        public int FlatSize;
        private UnsafeList<T> data;

        public UnsafeField(int2 size, Allocator allocator, T initialiseTo = default) {
            Size = size;
            FlatSize = size.x * size.y;
            data = new UnsafeList<T>(FlatSize, allocator);
            data.Length = FlatSize;
            InitialiseTo(initialiseTo);
        }

        public void InitialiseTo (T value) {
            for (int i = 0; i < FlatSize; i++) {
                data[i] = value;
            }
        }

        public T this[int i, int j] {
            get => data[i + j * Size.x];
            set => data[i + j * Size.x] = value;
        }

        public void Dispose() {
            data.Dispose();
        }

    }

}