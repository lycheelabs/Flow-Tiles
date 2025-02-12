using Unity.Mathematics;
using Unity.Collections;
using System;
using UnityEngine.InputSystem;

namespace FlowTiles.ECS {

    public struct LineCache {

        private NativeHashMap<int4, CachedSightline> Cache;

        public LineCache(int capacity) {
            Cache = new NativeHashMap<int4, CachedSightline>(capacity, Allocator.Persistent);
        }

        /// <summary> Returns whether the given line has been cached </summary>
        public bool ContainsLine(int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Returns the cached sightline data </summary>
        public bool TryGetSightline(int4 key, out CachedSightline existing) {
            existing = default;
            if (Cache.TryGetValue(key, out CachedSightline value)) {
                existing = value;
                return true;
            }
            return false;
        }

        /// <summary> Caches a line with the given key </summary>
        public void SetSightline(int4 key, CachedSightline line) {
            Cache[key] = line;
        }

        /// <summary> Clears all cached lines </summary>
        public void ClearAllLines () {
            Cache.Clear();
        }

        public void Dispose() {
            Cache.Dispose();
        }

    }

}