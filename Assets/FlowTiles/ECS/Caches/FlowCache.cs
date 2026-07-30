using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace LycheeLabs.FlowTiles.ECS {

    public struct FlowCache {

        private NativeHashMap<int4, CachedFlowField> Cache;
        private NativeHashMap<int, UnsafeList<int4>> Lookup;

        public FlowCache (int capacity) {
            Cache = new NativeHashMap<int4, CachedFlowField>(capacity, Allocator.Persistent);
            Lookup = new NativeHashMap<int, UnsafeList<int4>>(capacity, Allocator.Persistent);
        }

        public int Count => Cache.Count;

        /// <summary> Returns whether the given key has been cached </summary>
        public bool ContainsField (int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Retrieves a flow tile with the given key </summary>
        public bool TryGetField (int4 key, out CachedFlowField flowField) {
            if (Cache.TryGetValue(key, out flowField)) {
                if (!flowField.IsCreated) {
                    flowField = default;
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary> Caches a flow tile with the given sector and key </summary>
        public void StoreField (int sectorIndex, int4 key, CachedFlowField item) {

            // If key exists, replace existing data
            if (Cache.TryGetValue(key, out var existing)) {
                if (existing.IsCreated) {
                    existing.Dispose();
                }

                item.HasBeenQueued |= existing.HasBeenQueued;
                item.IsPending &= existing.IsPending;
            }
            // Else, add new key
            else {
                // Track so that ClearSector can dispose it later
                var hasLookup = Lookup.TryGetValue(sectorIndex, out var keys);
                if (!hasLookup) {
                    keys = new UnsafeList<int4>(PathfindingConstants.EXPECTED_MAX_EXITS, Allocator.Persistent);
                }
                keys.Add(key);
                Lookup[sectorIndex] = keys;
            }

            // Ensure queued placeholders always remain flagged as pending until real data arrives
            item.IsPending |= item.HasBeenQueued && !item.FlowField.IsCreated;
            Cache[key] = item;
        }

        /// <summary> Disposes all cached flow fields and their lookup lists, then clears both containers without destroying them. </summary>
        public void Clear () {
            var cacheValues = Cache.GetValueArray(Allocator.Temp);
            foreach (var value in cacheValues) {
                value.Dispose();
            }
            cacheValues.Dispose();
            Cache.Clear();

            var lookupValues = Lookup.GetValueArray(Allocator.Temp);
            foreach (var value in lookupValues) {
                if (value.IsCreated) {
                    value.Dispose();
                }
            }
            lookupValues.Dispose();
            Lookup.Clear();
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
            cacheValues.Dispose();

            var lookupValues = Lookup.GetValueArray(Allocator.Temp);
            foreach (var value in lookupValues) {
                if (value.IsCreated) {
                    value.Dispose();
                }
            }
            lookupValues.Dispose();

            if (Cache.IsCreated) {
                Cache.Dispose();
            }
            if (Lookup.IsCreated) {
                Lookup.Dispose();
            }
        }

    }

}