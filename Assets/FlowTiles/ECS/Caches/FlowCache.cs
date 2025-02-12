using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowTiles.ECS {

    public struct FlowCache {

        private NativeHashMap<int4, CachedFlowField> Cache;
        private NativeHashMap<int, UnsafeList<int4>> Lookup;

        public FlowCache (int capacity) {
            Cache = new NativeHashMap<int4, CachedFlowField>(capacity, Allocator.Persistent);
            Lookup = new NativeHashMap<int, UnsafeList<int4>>(capacity, Allocator.Persistent);
        }

        /// <summary> Returns whether the given key has been cached </summary>
        public bool ContainsField (int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Retrieves a flow tile with the given key </summary>
        public bool TryGetField (int4 key, out CachedFlowField flowField) {
            return Cache.TryGetValue(key, out flowField);
        }

        /// <summary> Caches a flow tile with the given sector and key </summary>
        public void StoreField (int sectorIndex, int4 key, CachedFlowField item) {
            if (TryGetField(key, out var existing)) {
                existing.Dispose();
                Cache[key] = item;
                return;
            }

            // Store key in lookup
            var hasLookup = Lookup.TryGetValue(sectorIndex, out var keys);
            if (!hasLookup) {
                keys = new UnsafeList<int4>(Constants.EXPECTED_MAX_EXITS, Allocator.Persistent);
            }
            keys.Add(key);
            Lookup[sectorIndex] = keys;

            // Store field in cache
            Cache[key] = item;
        }

        /// <summary> Clears all flow tiles for the given sector </summary>
        public void ClearSector (int sectorIndex) {
            var exists = Lookup.TryGetValue(sectorIndex, out var keys);
            if (exists) {
                foreach (var key in keys) {
                    var flowField = Cache[key];
                    flowField.Dispose();
                    Cache.Remove(key);
                }
                keys.Dispose();
                Lookup.Remove(sectorIndex);
            }
        }

        public void Dispose() {
            var cacheValues = Cache.GetValueArray(Allocator.Temp);
            foreach (var value in cacheValues) {
                value.Dispose();
            }
            var lookupValues = Lookup.GetValueArray(Allocator.Temp);
            foreach (var value in lookupValues) {
                value.Dispose();
            }
            Cache.Dispose();
            Lookup.Dispose();
        }

    }

}