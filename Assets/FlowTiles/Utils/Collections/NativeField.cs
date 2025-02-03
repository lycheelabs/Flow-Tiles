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

        public bool IsWithinBounds(int i) {
            return i >= 0 && i < FlatSize;
        }

        public bool IsWithinBounds(int x, int y) {
            return x >= 0 && y >= 0 && x < Size.x && y < Size.y;
        }

        public bool IsWithinBounds(float x, float y) {
            return x >= 0 && y >= 0 && x < Size.x && y < Size.y;
        }

        public bool IsWithinBounds(int2 cell) {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }

        public bool IsWithinBounds(float2 pos) {
            return pos.x >= 0 && pos.y >= 0 && pos.x < Size.x && pos.y < Size.y;
        }

        public T this[int i] {
            get => data[i];
            set => data[i] = value;
        }

        public T this[int x, int y] {
            get => data[x + y * Size.x];
            set => data[x + y * Size.x] = value;
        }

        public T this[int2 cell] {
            get => data[cell.x + cell.y * Size.x];
            set => data[cell.x + cell.y * Size.x] = value;
        }

        public T this[float x, float y] {
            get => data[(int)x + (int)y * Size.x];
            set => data[(int)x + (int)y * Size.x] = value;
        }

        public T this[float2 pos] {
            get => data[(int)math.floor(pos.x) + (int)math.floor(pos.y) * Size.x];
            set => data[(int)math.floor(pos.x) + (int)math.floor(pos.y) * Size.x] = value;
        }

        public void GetSafe(int i, ref T item) {
            if (IsWithinBounds(i)) {
                item = this[i];
            }
        }

        public void GetSafe(int x, int y, ref T item) {
            if (IsWithinBounds(x, y)) {
                item = this[x, y];
            }
        }

        public void GetSafe(int2 cell, ref T item) {
            if (IsWithinBounds(cell)) {
                item = this[cell];
            }
        }

        public void GetSafe(float x, float y, ref T item) {
            if (IsWithinBounds(x, y)) {
                item = this[x, y];
            }
        }

        public void GetSafe(float2 pos, ref T item) {
            if (IsWithinBounds(pos)) {
                item = this[pos];
            }
        }

        public void SetSafe(int i, T item) {
            if (IsWithinBounds(i)) {
                this[i] = item;
            }
        }

        public void SetSafe(int x, int y, T item) {
            if (IsWithinBounds(x, y)) {
                this[x, y] = item;
            }
        }

        public void SetSafe(int2 cell, T item) {
            if (IsWithinBounds(cell)) {
                this[cell] = item;
            }
        }

        public void SetSafe(float x, float y, T item) {
            if (IsWithinBounds(x, y)) {
                this[x, y] = item;
            }
        }

        public void SetSafe(float2 pos, T item) {
            if (IsWithinBounds(pos)) {
                this[pos] = item;
            }
        }

        public void Dispose() {
            data.Dispose();
        }

    }

}