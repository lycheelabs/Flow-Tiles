// Native priority queue implementation based on...
// https://gist.github.com/StagPoint/02a845585f6900a48e9035b00f07726e
// Copyright 2017-2021 StagPoint Software

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace LycheeLabs.FlowTiles.Utils {

    /// <summary>
    /// Priority Queue implementation with item data stored in native containers. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativePriorityQueueDebugView<>))]
    public unsafe struct NativePriorityQueue<T> : IDisposable
        where T : struct, IComparable<T> {
        #region Public properties

        public int Length {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return m_ListData != null ? m_ListData->length : 0;
            }
        }

        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_ListData == null || m_ListData->length == 0; }
        }

        #endregion

        #region Private fields
        private const int GROWTH_FACTOR = 2;

        private Allocator m_AllocatorLabel;
        private NativeArray<T> m_Buffer;

        [NativeDisableUnsafePtrRestriction]
        private UnsafeListData* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        [NativeSetClassTypeToNullOnSchedule] internal DisposeSentinel m_DisposeSentinel;
#endif

        #endregion

        #region Constructor

        public NativePriorityQueue(int capacity, Allocator allocator) {
            if (capacity < 1) {
                throw new ArgumentException("Capacity must be greater than zero");
            }

            m_AllocatorLabel = allocator;

            m_ListData = (UnsafeListData*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<UnsafeListData>(), UnsafeUtility.AlignOf<UnsafeListData>(), allocator);
            m_Buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);

            m_ListData->capacity = capacity;
            m_ListData->length = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#endif
        }

        #endregion

        #region Public methods

        public bool IsCreated => m_Buffer.IsCreated;

        public void Dispose() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif

            if (!m_Buffer.IsCreated || m_ListData == null) {
                return; // already disposed
            }

            UnsafeUtility.Free(m_ListData, m_AllocatorLabel);
            m_Buffer.Dispose();

            m_ListData = null;
            m_AllocatorLabel = Allocator.Invalid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            if (m_ListData == null) return;
            m_ListData->length = 0;
            for (int i = 0; i < m_Buffer.Length; i++) {
                m_Buffer[i] = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek() {
            if (m_ListData == null || m_ListData->length == 0) {
                throw new InvalidOperationException("Cannot peek at first item when the heap is empty.");
            }
            return m_Buffer[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item) {
            if (m_ListData == null) return;

            if (m_ListData->length == m_ListData->capacity) {
                EnsureCapacity(m_ListData->length + 1);
            }

            m_Buffer[m_ListData->length] = item;
            heapifyUp(m_ListData->length, item);
            m_ListData->length++;
        }

        public T Dequeue() {
            if (m_ListData == null || m_ListData->length == 0) {
                throw new InvalidOperationException("Cannot remove item from an empty heap");
            }

            var v = m_Buffer[0];
            m_ListData->length--;

            m_Buffer[0] = m_Buffer[m_ListData->length];
            m_Buffer[m_ListData->length] = default(T);

            heapifyDown(0, m_Buffer[0]);

            return v;
        }

        public void EnsureCapacity(int count) {
            if (m_ListData == null) return;

            var originalCapacity = m_ListData->capacity;
            var originalLength = m_ListData->length;

            while (count > m_ListData->capacity) {
                m_ListData->capacity *= GROWTH_FACTOR;
            }

            var newArray = new NativeArray<T>(m_ListData->capacity, m_AllocatorLabel, NativeArrayOptions.UninitializedMemory);

            if (originalLength > 0) {
                var dataSize = UnsafeUtility.SizeOf<T>() * originalLength;
                UnsafeUtility.MemCpy(newArray.GetUnsafePtr(), m_Buffer.GetUnsafePtr(), dataSize);
            }

            m_Buffer.Dispose();
            m_Buffer = newArray;
        }

        public T[] ToArray() {
            if (m_ListData == null) return new T[0];

            var length = m_ListData->length;
            T[] result = new T[length];
            for (int i = 0; i < length; i++) {
                result[i] = m_Buffer[i];
            }

            return result;
        }

        #endregion

        #region Heap methods

        private int heapifyUp(int index, T item) {
            var parent = (index - 1) >> 1;

            while (parent > -1 && item.CompareTo(m_Buffer[parent]) <= 0) {
                m_Buffer[index] = m_Buffer[parent];
                index = parent;
                parent = (index - 1) >> 1;
            }

            m_Buffer[index] = item;
            return index;
        }

        private int heapifyDown(int parent, T item) {
            var index = 0;

            while (true) {
                int ch1 = (parent << 1) + 1;
                if (ch1 >= m_ListData->length)
                    break;

                int ch2 = (parent << 1) + 2;
                if (ch2 >= m_ListData->length) {
                    index = ch1;
                }
                else {
                    index = m_Buffer[ch1].CompareTo(m_Buffer[ch2]) <= 0 ? ch1 : ch2;
                }

                if (item.CompareTo(m_Buffer[index]) < 0)
                    break;

                m_Buffer[parent] = m_Buffer[index];
                parent = index;
            }

            m_Buffer[parent] = item;
            return parent;
        }

        #endregion

        #region Debugging support

        public override string ToString() {
            return string.Format("Length={0}", m_ListData != null ? m_ListData->length : -1);
        }

        #endregion
    }

    #region Related types 

    public struct UnsafeListData {
        public int length;
        public int capacity;
    }

    internal sealed class NativePriorityQueueDebugView<T>
        where T : struct, IComparable<T> {
        private NativePriorityQueue<T> list;

        public NativePriorityQueueDebugView(NativePriorityQueue<T> list) {
            this.list = list;
        }

        public T[] Items {
            get {
                return list.ToArray();
            }
        }
    }

    #endregion
}
