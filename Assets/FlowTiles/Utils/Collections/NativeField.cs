using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.Utils {

    public struct NativeField<T> where T : unmanaged {

        public readonly int2 Size;
        public int FlatSize;
        private NativeArray<T> data;

        public NativeField(int2 size, Allocator allocator, T initialiseTo = default) {
            Size = size;
            FlatSize = size.x * size.y;
            data = new NativeArray<T>(FlatSize, allocator);

            if (!initialiseTo.Equals(default)) {
                InitialiseTo(initialiseTo);
            }
        }

        public bool IsCreated => data.IsCreated;

        public void InitialiseTo(T value) {
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