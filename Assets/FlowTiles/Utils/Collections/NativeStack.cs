///////////////////////////////////////////////////////////////////////////////////////
// Native queue collection with fixed length. FILO.
// NativeStack<T> supports writing in IJobParallelFor, just use ParallelWriter and AsParallelWriter().
// Not tested to much, works for me with integers and unity 2019.3
//
// Assembled with help of:
// - NativeCounter example (https://docs.unity3d.com/Packages/com.unity.jobs@0.1/manual/custom_job_types.html)
// - How to Make Custom Native Collections (https://jacksondunstan.com/articles/4734)
// - source code of NativeQueue and NativeList

/*
MIT License
Copyright 2018 Jackson Dunstan
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, 
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
*/

using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace FlowTiles.Utils {

    public unsafe struct NativeStack<T> : IDisposable where T : struct {
        private NativeArray<T> m_Array;
        [NativeDisableUnsafePtrRestriction] int* m_Counter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;

        // The dispose sentinel tracks memory leaks. It is a managed type so it is cleared to null when scheduling a job
        // The job cannot dispose the container, and no one else can dispose it until the job has run, so it is ok to not pass it along
        // This attribute is required, without it this NativeContainer cannot be passed to a job; since that would give the job access to a managed object
        [NativeSetClassTypeToNullOnSchedule] DisposeSentinel m_DisposeSentinel;
#endif

        // Keep track of where the memory for this was allocated
        Allocator m_AllocatorLabel;

        public NativeStack(int length, Allocator label) {
            // This check is redundant since we always use an int that is blittable.
            // It is here as an example of how to check for type correctness for generic types.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            /*if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(
                    string.Format("{0} used in NativeStack<{0}> must be blittable", typeof(T)));*/
#endif
m_AllocatorLabel = label;

            // Allocate native memory for a single integer
            m_Counter = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>(), 4, label);
            m_Array = new NativeArray<T>(length, label);

            // Create a dispose sentinel to track memory leaks. This also creates the AtomicSafetyHandle
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, label);
#endif
            // Initialize the count to 0 to avoid uninitialized data
            *m_Counter = 0;
        }

        public bool IsCreated {
            get { return m_Counter != null; }
        }

        public int Count {
            get {
                // Verify that the caller has read permission on this data. 
                // This is the race condition protection, without these checks the AtomicSafetyHandle is useless
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return *m_Counter;
            }
        }

        void Deallocate() {
            UnsafeUtility.Free(m_Counter, m_AllocatorLabel);
            m_Array.Dispose();
            m_Counter = null;
        }

        public void Dispose() {
            // Let the dispose sentinel know that the data has been freed so it does not report any memory leaks
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            Deallocate();
        }

        public void Push(T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if (*m_Counter >= m_Array.Length) {
                throw new IndexOutOfRangeException(
                    "Can't push, stack is full. Capacity: " + m_Array.Length);
            }
#endif
            m_Array[*m_Counter] = item;
            (*m_Counter)++;
        }

        public T Pop() {
            T item;

            if (!TryPop(out item))
                throw new InvalidOperationException("Trying to pop from an empty stack.");

            return item;
        }

        public bool TryPop(out T item) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            if (*m_Counter <= 0) {
                item = default;
                return false;
            }

            (*m_Counter)--;
            item = m_Array[*m_Counter];
            return true;
        }

        public void Clear() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            *m_Counter = 0;
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run.</remarks>
        /// <param name="jobHandle">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        public JobHandle Dispose(JobHandle inputDeps) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            m_Counter = null;

            return jobHandle;
        }

        [BurstCompile]
        struct DisposeJob : IJob {
            public NativeStack<T> Container;

            public void Execute() {
                Container.Deallocate();
            }
        }

        public ParallelWriter AsParallelWriter() {
            ParallelWriter writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            writer.m_Safety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref writer.m_Safety);
#endif
            writer.m_Counter = m_Counter;
            writer.m_Array = m_Array;

            return writer;
        }

        /// <summary>
        /// Implements parallel writer. Use AsParallelWriter to obtain it from container.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriter {
            [NativeDisableUnsafePtrRestriction]
            internal int* m_Counter;

            [WriteOnly]
            [NativeDisableParallelForRestriction]
            internal NativeArray<T> m_Array;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif

            public void Push(T entry) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if (*m_Counter >= m_Array.Length)
                    throw new IndexOutOfRangeException("Can't push, stack is full.");
#endif

                int index = Interlocked.Increment(ref *m_Counter) - 1;
                m_Array[index] = entry;
            }
        }
    }
}