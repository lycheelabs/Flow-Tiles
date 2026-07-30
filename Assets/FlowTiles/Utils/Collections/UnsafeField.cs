using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace LycheeLabs.FlowTiles.Utils {

    public struct UnsafeField<T> where T : unmanaged {

        public readonly int2 Size;
        public int FlatSize;
        private UnsafeList<T> data;

        public UnsafeField(int2 size, Allocator allocator, T initialiseTo = default) {
            if (size.x < 0 || size.y < 0 || (long)size.x * size.y > int.MaxValue) {
                throw new ArgumentException($"UnsafeField size {size} would cause integer overflow");
            }

            Size = size;
            FlatSize = size.x * size.y;
            data = new UnsafeList<T>(FlatSize, allocator);
            data.Length = FlatSize;

            InitialiseTo(initialiseTo);
        }

        public bool IsCreated => data.IsCreated;

        private void EnsureCreated() {
            if (!data.IsCreated) {
                throw new InvalidOperationException("UnsafeField has not been created or has already been disposed");
            }
        }

        public void InitialiseTo (T value) {
            EnsureCreated();
            for (int i = 0; i < FlatSize; i++) {
                data[i] = value;
            }
        }

        public T this[int i, int j] {
            get {
                EnsureCreated();
                if (!IsValidIndex(i, j)) {
                    throw new IndexOutOfRangeException();
                }
                return data[i + j * Size.x];
            }
            set {
                EnsureCreated();
                if (!IsValidIndex(i, j)) {
                    throw new IndexOutOfRangeException();
                }
                data[i + j * Size.x] = value;
            }
        }

        public void Dispose() {
            if (data.IsCreated) {
                data.Dispose();
                data = default; 
            }
        }

        public bool IsValidIndex(int x, int y) {
            return x >= 0 && x < Size.x && y >= 0 && y < Size.y;
        }

    }

}