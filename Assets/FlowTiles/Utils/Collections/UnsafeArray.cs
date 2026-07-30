using System.Drawing;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace LycheeLabs.FlowTiles.Utils {

    public struct UnsafeArray<T>  where T : unmanaged {

        public readonly int Length;
        private UnsafeList<T> data;

        public UnsafeArray(int size, Allocator allocator, T initialiseTo = default) {
            // CRITICAL FIX: Check for negative size to prevent TLSF corruption
            if (size < 0) {
                throw new ArgumentException($"UnsafeArray size {size} cannot be negative");
            }
            Length = size;
            data = new UnsafeList<T>(size, allocator);
            data.Length = size;
            for (int i = 0; i < size; i++) {
                data[i] = initialiseTo;
            }
        }

        public bool IsCreated => data.IsCreated;

        private void EnsureCreated() {
            if (!data.IsCreated) {
                throw new InvalidOperationException("UnsafeArray has not been created or has already been disposed");
            }
        }

        public T this[int i] {
            get {
                EnsureCreated();
                if (i < 0 || i >= Length) {
                    throw new IndexOutOfRangeException();
                }
                return data[i];
            }
            set {
                EnsureCreated();
                if (i < 0 || i >= Length) {
                    throw new IndexOutOfRangeException();
                }
                data[i] = value;
            }
        }

        public void Dispose () {
            // CRITICAL FIX: Prevent double disposal that can cause TLSF corruption
            if (data.IsCreated) {
                data.Dispose();
                data = default; // Mark as disposed
            }
        }

    }

}