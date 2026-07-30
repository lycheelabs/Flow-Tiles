using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.ECS {

    [BurstCompile]
    public partial struct RequestPathsJob : IJobEntity {

        public PathCache PathCache;
        public NativeQueue<PathRequest> PathRequests;
        public NativeHashSet<int4> PathInFlightKeys;

        public EntityCommandBuffer ECB;

        [BurstCompile]
        private void Execute(RefRO<MissingPathData> data, Entity entity) {

            var key = data.ValueRO.Key;

            // Enqueue at most once per unique key. PathInFlightKeys.Add returns false if the
            // key is already present, so duplicate requests from re-emitting agents are dropped
            // here instead of polluting the queue. The set entry is removed by
            // ProcessPathRequests when the request is promoted to a real task (or recognised as
            // a duplicate of an already-completed path).
            if (!PathCache.ContainsPath(key) && PathInFlightKeys.Add(key)) {
                PathRequests.Enqueue(new PathRequest {
                    originCell = data.ValueRO.Start,
                    destCell = data.ValueRO.Dest,
                    levelSize = data.ValueRO.LevelSize,
                    travelType = data.ValueRO.TravelType,
                });
            }

            // Remove component
            ECB.RemoveComponent<MissingPathData>(entity);

        }
    }

}

