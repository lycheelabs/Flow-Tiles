using Unity.Mathematics;
using Unity.Collections;

namespace FlowTiles.ECS {

    public struct PathCache {

        private int Capacity;
        private NativeHashMap<int4, CachedPortalPath> Cache;
        private NativeQueue<int4> KeyQueue;

        public PathCache(int capacity) {
            Capacity = capacity;
            Cache = new NativeHashMap<int4, CachedPortalPath>(capacity, Allocator.Persistent);
            KeyQueue = new NativeQueue<int4>(Allocator.Persistent);
        }

        public int Count => KeyQueue.Count;
        
        public NativeArray<CachedPortalPath> GetAllValues (Allocator allocator) {
            return Cache.GetValueArray(allocator);
        }

        /// <summary> Returns whether the given key has been cached </summary>
        public bool ContainsPath(int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Retrieves a flow tile with the given key </summary>
        public bool TryGetPath(int4 key, out CachedPortalPath path) {
            if (Cache.TryGetValue(key, out path)) {
                if (!path.IsCreated) {
                    path = default;
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary> 
        /// If the oldest key is still pending, dont try storing new paths.
        /// It would create churn that stops any paths from finishing. 
        /// </summary>
        public bool WaitForCapacity (float now, float pendingTimeoutSeconds) {
            if (Cache.Count < Capacity) {
                return false;
            }

            var oldest = KeyQueue.Peek();
            if (!Cache.ContainsKey(oldest)) {
                KeyQueue.Dequeue();
                return false;
            }

            // Check pending
            var cached = Cache[oldest];
            if (!cached.IsPending) {
                return false;
            }

            // Check pending timeout
            if (pendingTimeoutSeconds > 0f && cached.PendingSinceTime > 0f
                && now - cached.PendingSinceTime >= pendingTimeoutSeconds) {
                KeyQueue.Dequeue();
                TryDisposePath(oldest);
                return false;
            }

            return true;
        }

        /// <summary> Caches a path with the given key </summary>
        public void StorePath(int4 key, CachedPortalPath item) {

            // If full, deallocate the oldest
            if (Cache.Count >= Capacity) {
                while (KeyQueue.Count > 0) {
                    var oldest = KeyQueue.Dequeue();
                    if (TryDisposePath(oldest)) {
                        break;
                    }
                }
            }

            // If key exists, merge data
            if (Cache.TryGetValue(key, out var existing)) {
                if (existing.IsCreated) {
                    existing.Dispose();
                }
                item.HasBeenQueued |= existing.HasBeenQueued;
            }

            // Else, add new key
            else {
                KeyQueue.Enqueue(key);
            }

            Cache[key] = item;

        }

        /// <summary> Disposes and removes the path with the given key </summary>
        public void DisposePath(int4 key) {
            TryDisposePath(key);
        }

        /// <summary> Disposes all cached paths and clears both containers without destroying them. </summary>
        public void Clear () {
            if (Cache.IsCreated) {
                var cacheValues = Cache.GetValueArray(Allocator.Temp);
                foreach (var value in cacheValues) {
                    value.Dispose();
                }
                cacheValues.Dispose();
                Cache.Clear();
            }
            if (KeyQueue.IsCreated) {
                while (KeyQueue.TryDequeue(out _)) { }
            }
        }

        public void Dispose() {
            if (Cache.IsCreated) {
                var cacheValues = Cache.GetValueArray(Allocator.Temp);
                foreach (var value in cacheValues) {
                    value.Dispose();
                }
                Cache.Dispose();
            }
            if (KeyQueue.IsCreated) {
                KeyQueue.Dispose();
            }
        }

        private bool TryDisposePath(int4 key) {
            if (!Cache.TryGetValue(key, out var cached)) {
                return false;
            }

            if (cached.IsCreated) {
                cached.Dispose();
            }

            Cache.Remove(key);
            return true;
        }

    }

}