using FlowTiles.PortalGraphs;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles {

    public partial struct PathfindingSystem : ISystem {

        private PathCache PathCache;
        private FlowCache FlowCache;

        //private Entity BufferSingleton;
        private NativeList<PathRequest> PathRequests;
        private NativeList<FlowRequest> FlowRequests;

        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<GlobalPathfindingData>();

            // Build the caches
            PathCache = new PathCache {
                Cache = new NativeParallelHashMap<int4, PortalPath>(1000, Allocator.Persistent) 
            };
            FlowCache = new FlowCache {
                Cache = new NativeParallelHashMap<int4, FlowFieldTile>(1000, Allocator.Persistent)
            };

            // Build the request buffers
            PathRequests = new NativeList<PathRequest>(1000, Allocator.Persistent);
            FlowRequests = new NativeList<FlowRequest>(1000, Allocator.Persistent);

        }

        public void OnUpdate(ref SystemState state) {

            // Access the global data
            var globalData = SystemAPI.GetSingleton<GlobalPathfindingData>();
            var portalGraph = globalData.Graph;

            // Process pathfinding requests, and cache the results
            foreach (var request in PathRequests) {
                //UnityEngine.Debug.Log("Creating path: " + request.cacheKey);
                var path = PortalPathfinder.FindPortalPath(portalGraph, request.originCell, request.destCell);
                PathCache.Cache[request.cacheKey] = new PortalPath {
                    Path = path
                };
            }
            PathRequests.Clear();

            // TODO: Process flow field requests, and cache the results
            foreach (var request in FlowRequests) {
                UnityEngine.Debug.Log("Requested flow: " + request.cacheKey);
            }
            FlowRequests.Clear();

            // Search for entity paths
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            new RequestPathsJob {
                CostMap = portalGraph.Costs,
                PathCache = PathCache.Cache,
                FlowCache = FlowCache.Cache,
                PathRequests = PathRequests,
                FlowRequests = FlowRequests,
            }.Schedule();

        }

        public void OnDestroy (ref SystemState state) {
            PathCache.Cache.Dispose();
            FlowCache.Cache.Dispose();
            PathRequests.Dispose();
            FlowRequests.Dispose();
        }

        [BurstCompile]
        public partial struct RequestPathsJob : IJobEntity {

            public CostMap CostMap;
            public NativeParallelHashMap<int4, PortalPath> PathCache;
            public NativeParallelHashMap<int4, FlowFieldTile> FlowCache;

            public NativeList<PathRequest> PathRequests;
            public NativeList<FlowRequest> FlowRequests;

            [BurstCompile]
            private void Execute(PathfindingData agent, [ChunkIndexInQuery] int sortKey) {
                var origin = agent.OriginCell;
                var originSector = CostMap.GetSectorIndex(origin.x, origin.y);
                var originColor = CostMap.GetColor(origin.x, origin.y);

                var dest = agent.DestCell;
                var destCell = CostMap.GetCellIndex(dest.x, dest.y);
                var destColor = CostMap.GetColor(dest.x, dest.y);

                // Search for a cached path
                var pathKey = new int4(originSector, originColor, destCell, destColor);
                var pathCacheHit = PathCache.ContainsKey(pathKey);
                if (!pathCacheHit) {

                    // Request a path be generated
                    PathRequests.Add(new PathRequest {
                        originCell = agent.OriginCell,
                        destCell = agent.DestCell,
                        cacheKey = pathKey,
                    });
                    return;
                }

                var pathNodes = PathCache[pathKey].Path;
                if (pathNodes.Length > 0) {
                    var firstNode = pathNodes[0];
                    var flowKey = firstNode.CacheKey;
                    var flowCacheHit = FlowCache.ContainsKey(flowKey);
                    if (!flowCacheHit) {

                        // Request a flow be generated
                        FlowRequests.Add(new FlowRequest {
                            goalCell = firstNode.Position.Cell,
                            goalDirection = firstNode.Direction,
                        });
                        return;
                    }

                    var flow = FlowCache[flowKey];
                    UnityEngine.Debug.Log("Retrieved flow");
                }

            }
        }
    }

}