using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowTiles.Utils {

    public struct UnsafeArray<T>  where T : unmanaged {

        public readonly int Size;
        private UnsafeList<T> data;

        public UnsafeArray(int size, Allocator allocator) {
            Size = size;
            data = new UnsafeList<T>(size, allocator);
            data.Length = size;
            for (int i = 0; i < size; i++) {
                data[i] = default;
            }
        }

        public T this[int i] {
            get => data[i];
            set => data[i] = value;
        }

        public void Dispose () {
            data.Dispose();
        }

    }

}