using Unity.Mathematics;
using Unity.Collections;
using System;

namespace FlowTiles.ECS {

    public struct PathCache {

        private NativeParallelHashMap<int4, CachedPortalPath> Cache;
        private NativeQueue<int4> Queue;

        public PathCache(int capacity) {
            Cache = new NativeParallelHashMap<int4, CachedPortalPath>(capacity, Allocator.Persistent);
            Queue = new NativeQueue<int4>(Allocator.Persistent);
        }

        /// <summary> Returns whether the given key has been cached </summary>
        public bool ContainsPath(int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Retrieves a flow tile with the given key </summary>
        public bool TryGetPath(int4 key, out CachedPortalPath path) {
            return Cache.TryGetValue(key, out path);
        }

        /// <summary> Caches a path with the given key </summary>
        public void StorePath(int4 key, CachedPortalPath item) {

            // If full, deallocate the oldest
            if (Cache.Count() >= Cache.Capacity) {
                while (Queue.Count > 0) {
                    var last = Queue.Dequeue();
                    if (Cache.TryGetValue(last, out var lastPath)) {
                        lastPath.Dispose();
                        Cache.Remove(last);
                        break;
                    }
                }
            }

            // If the key exists, replace the path
            if (TryGetPath(key, out var existing)) {
                existing.Dispose();
                Cache[key] = item;
                return;
            }

            // Add to the cache
            Cache[key] = item;
            Queue.Enqueue(key);

        }

        /// <summary> Disposes and removes the path with the given key </summary>
        public void DisposePath(int4 key) {
            if (Cache.ContainsKey(key)) {
                Cache[key].Dispose();
                Cache.Remove(key);
            }
        }

        public void Dispose() {
            var cacheValues = Cache.GetValueArray(Allocator.Temp);
            foreach (var value in cacheValues) {
                value.Dispose();
            }
            Cache.Dispose();
            Queue.Dispose();
        }

    }

}