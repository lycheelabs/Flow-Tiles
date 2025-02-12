using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowTiles.ECS {

    public struct LineCache {

        private NativeHashMap<int4, bool> Cache;

        public LineCache(int capacity) {
            Cache = new NativeHashMap<int4, bool>(capacity, Allocator.Persistent);
        }

        /// <summary> Returns whether the given line has been cached </summary>
        public bool ContainsLine(int4 key) {
            return Cache.ContainsKey(key);
        }

        /// <summary> Returns true if an open line of sight exists </summary>
        public bool LineOfSightExists(int4 key) {
            if (Cache.TryGetValue(key, out bool value)) {
                return value;
            }
            return false;
        }

        /// <summary> Caches a line with the given key </summary>
        public void SetLineOfSight(int4 key, bool lineExists) {
            Cache[key] = lineExists;
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