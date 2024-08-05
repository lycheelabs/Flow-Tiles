using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.Utils {

    public struct NativeField<T> where T : unmanaged {

        public readonly int2 Size;
        public int FlatSize;
        private NativeArray<T> data;

        public NativeField(int2 size, Allocator allocator) {
            Size = size;
            FlatSize = size.x * size.y;
            data = new NativeArray<T>(FlatSize, allocator);
        }

        public NativeField(int2 size, Allocator allocator, T initialiseTo) {
            Size = size;
            FlatSize = size.x * size.y;
            data = new NativeArray<T>(FlatSize, allocator);
            InitialiseTo(initialiseTo);
        }

        public bool IsCreated => data.IsCreated;

        public void InitialiseTo(T value) {
            for (int i = 0; i < FlatSize; i++) {
                data[i] = value;
            }
        }

        public T this[int x, int y] {
            get => data[x + y * Size.x];
            set => data[x + y * Size.x] = value;
        }

        public T this[int i] {
            get => data[i];
            set => data[i] = value;
        }

        public void Dispose() {
            data.Dispose();
        }

    }

}