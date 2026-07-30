using System;
using Unity.Collections;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.Utils {

    public struct NativeField<T> where T : unmanaged {

        public readonly int2 Size;
        public int FlatSize;
        private NativeArray<T> data;

        public NativeField(int2 size, Allocator allocator) {
            Size = size;
            // CRITICAL FIX: Check for integer overflow to prevent TLSF corruption
            if (size.x < 0 || size.y < 0 || (long)size.x * size.y > int.MaxValue) {
                throw new ArgumentException($"NativeField size {size} would cause integer overflow");
            }
            FlatSize = size.x * size.y;
            data = new NativeArray<T>(FlatSize, allocator);
        }

        public NativeField(int2 size, Allocator allocator, T initialiseTo) {
            Size = size;
            // CRITICAL FIX: Check for integer overflow to prevent TLSF corruption
            if (size.x < 0 || size.y < 0 || (long)size.x * size.y > int.MaxValue) {
                throw new ArgumentException($"NativeField size {size} would cause integer overflow");
            }
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

        public bool IsWithinBounds(int2 cell) {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;
        }

        public T this[int i] {
            get {
                if (i < 0 || i >= FlatSize)
                    throw new IndexOutOfRangeException();
                return data[i];
            }
            set {
                if (i < 0 || i >= FlatSize)
                    throw new IndexOutOfRangeException();
                data[i] = value;
            }
        }

        public T this[int x, int y] {
            get {
                if (!IsWithinBounds(x, y))
                    throw new IndexOutOfRangeException();
                return data[x + y * Size.x];
            }
            set {
                if (!IsWithinBounds(x, y))
                    throw new IndexOutOfRangeException();
                data[x + y * Size.x] = value;
            }
        }

        public T this[int2 cell] {
            get {
                if (!IsWithinBounds(cell))
                    throw new IndexOutOfRangeException();
                return data[cell.x + cell.y * Size.x];
            }
            set {
                if (!IsWithinBounds(cell))
                    throw new IndexOutOfRangeException();
                data[cell.x + cell.y * Size.x] = value;
            }
        }

        public T this[float x, float y] {
            get {
                int i = (int)math.floor(x);
                int j = (int)math.floor(y);
                if (!IsWithinBounds(i, j))
                    throw new IndexOutOfRangeException();
                return data[i + j * Size.x];
            }
            set {
                int i = (int)math.floor(x);
                int j = (int)math.floor(y);
                if (!IsWithinBounds(i, j))
                    throw new IndexOutOfRangeException();
                data[i + j * Size.x] = value;
            }
        }

        public T this[float2 pos] {
            get {
                int i = (int)math.floor(pos.x);
                int j = (int)math.floor(pos.y);
                if (!IsWithinBounds(i, j))
                    throw new IndexOutOfRangeException();
                return data[i + j * Size.x];
            }
            set {
                int i = (int)math.floor(pos.x);
                int j = (int)math.floor(pos.y);
                if (!IsWithinBounds(i, j))
                    throw new IndexOutOfRangeException();
                data[i + j * Size.x] = value;
            }
        }

        public void Dispose() {
            if (data.IsCreated) {
                data.Dispose();
            }
        }

    }

}