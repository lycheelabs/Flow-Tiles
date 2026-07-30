# LycheeLabs.FlowTiles � Architecture Overview

A Unity DOTS pathfinding system that combines **Hierarchical Pathfinding A\*** (HPA) with
**per-tile flow fields**. Designed to scale to thousands of agents by spreading work across
frames, caching results, and running every heavy computation as a Burst-compiled job.

---

## 1. The big picture

```
            world grid                                agents (DOTS entities)
            ????????                                  ?????????????????????
          PathableGrid                                 FlowGoal + FlowPosition
                ?                                              ?
                ? (dirty sectors)                              ? "I need a direction"
                ?                                              ?
          PathableGraph         ????? reads ????         FollowPathsJob
        (sectorised graph)                                     ?
                                                               ? adds Missing*Data tags
                                                               ?
                                                     Request*Jobs (single-thread)
                                                               ?
                                                               ?
                                                       request queues
                                                               ?
                                                               ?
                                              PathfindingSystem.OnUpdate
                                                  schedules per-frame jobs
                                                  ???????????????????????????
                                                  ?            ?            ?
                                            FindPathsJob  FindFlowsJob  FindSightlinesJob
                                                  ?            ?            ?
                                                  ?            ?            ?
                                              PathCache    FlowCache    LineCache
```

The system has three layers of data:

1. **Grid layer** (`PathableGrid`): the raw walkable/blocked map plus terrain & dynamic costs.
2. **Graph layer** (`PathableGraph`): the level cut into fixed-size **sectors**
   (`Resolution � Resolution`, but the row/column at the right/top edges of the level may
   be clipped to the level bounds � see `SectorLayout.GetSectorBounds`). Each sector holds
   *islands* (connected-component IDs), *exit portals* between adjacent sectors, and the
   *edges* between those portals. Used for the high-level HPA\* search.
3. **Flow layer** (`FlowField`, one per `(sector, goal)`): a Dijkstra-derived 2D field of
   per-cell direction vectors that an agent follows once it has chosen which sector to be in.

Pathfinding is a **producer/consumer** pipeline: agents emit "I'm missing X" tags, those
become enqueued requests, the system processes a bounded number of requests per frame in
parallel jobs, and results are committed to caches that the agents read from on subsequent
frames.

---

## 2. Data model

### 2.1 `PathableGrid` (`Level/PathableGrid.cs`)
- 2D cost grid (`Blocked`, `Terrain`, `TerrainAdjustments`, `Obstacles`).
- `GetCostAt(x, y, travelType)` returns a per-travel-type cost. `MAX_COST` means impassable.
- `RebuildFlags[sectorIndex]` marks sectors that need to be regenerated.
- `UpdateRebuildFlags(corner, size)` expands the dirty rect by 1 cell so a change near a
  sector boundary marks both adjacent sectors.

### 2.2 `PathableGraph` and `GraphSector` (`PortalPaths/PortalGraph/`)
- Layout (`SectorLayout`) gives each sector a fixed `Resolution � Resolution` cell area.
- One `GraphSector` per sector index, containing one `SectorData` per travel type:
  - `SectorCosts` � local copy of cost grid for the sector.
  - `SectorIslands` � connected-component map (per cell, 1?based island IDs).
  - `SectorPortals` � `Roots[]` (one per island) and `Exits[]` (border crossings between
    sectors), plus an `ExitLookup` hashmap from cell ? exit index.
- `Portal` holds its centre cell, its bounding rect, an `Island` (which island it belongs
  to inside the sector), a `Continent` (cross-sector connected-component ID assigned by
  the `ContinentPathfinder`), and a list of `PortalEdge` connections.
- `PortalEdge.start/end` are `SectorCell`s � each edge either lives **inside** a sector
  (connecting two of that sector's exits via an internal A\* through the cost grid) or
  **spans** two sectors (connecting an exit to its mirror exit in the neighbour).

### 2.3 Continents (`PortalPaths/ContinentPathfinder.cs`)
After every full graph rebuild, `RecalculateContinents` BFSs through the portal graph and
labels every root and every exit with a `Continent` id. Two cells with different continent
ids are guaranteed to have no path between them � the agent can short-circuit immediately.

### 2.4 `FlowField` (`FlowFields/FlowField.cs`)
A small per-sector grid holding:
- `Directions[x,y]` � `float2` flow vector to follow at that cell.
- `Distances[x,y]` � int distance to the goal (used by `PortalPathfinder` to evaluate
  edge weights for HPA\*).
- A `Version` matching the `SectorData.Version` it was built from. Mismatch ? stale.

`FlowField`s are computed by `FlowCalculator` (a Dijkstra wavefront from the goal cells).

---

## 3. ECS components on each agent

| Component | Role |
| --- | --- |
| `FlowPosition` | Smooth world position + integer cell. |
| `FlowGoal` | Current target cell, travel type, smoothing mode. |
| `FlowDirection` | Output: the direction the agent should currently move in. |
| `FlowProgress` | Persistent state: which path key, which node along it, which flow key, sightline keys. |
| `MissingPathData` | Tag added when the agent needs a path that isn't cached yet. |
| `MissingFlowData` | Same, but for a flow tile. |
| `MissingSightlineData` | Same, but for a sightline check. |
| `InvalidPathData` | Tag asking the system to evict a specific cached path. |
| `FlowDebugData` | Editor-only visualisation data. |

---

## 4. Caches (`ECS/Caches/`)

All three caches are `NativeHashMap<int4, T>` keyed by deterministic `int4` keys built in
`CacheKeys`.

- **`PathCache`** � `CachedPortalPath`s. Bounded to `MAX_CACHED_PATHS` (500). Keeps a FIFO
  `KeyQueue` so that when the cache is full, the oldest entry is evicted to make room.
  `WaitForCapacity` provides back-pressure: if the cache is full and the oldest entry is
  *still pending* (its job hasn't finished yet), `StorePath` is delayed so we don't churn.
  After a configurable timeout (3 s) a stuck pending entry is forcibly evicted.
- **`FlowCache`** � `CachedFlowField`s. **Has no size cap and no LRU eviction**; the
  initial `NativeHashMap` capacity (`EXPECTED_SECTORS_IN_MAP * 10` = 500) is just a
  starting hint and the map will grow as new flow keys arrive. The only mechanism that
  removes entries is `ClearSector(sectorIndex)`, called when a sector rebuilds.
  `Lookup[sectorIndex]` remembers all flow keys belonging to a sector for fast
  invalidation.
- **`LineCache`** � `CachedSightline`s. Versioned by graph version.

A `Cached*` value carries two boolean flags worth highlighting:
- `HasBeenQueued` � a request has been sent for this key (avoids duplicate scheduling).
- `IsPending` � the result is being computed in a job and shouldn't be read yet.

---

## 5. The frame loop (`ECS/Systems/PathfindingSystem.cs`)

`PathfindingSystem.OnUpdate` runs once per frame in `FrameStartSystemGroup` (`OrderLast`).
It is a **pipeline** with a single `state.Dependency` chain so jobs scheduled in earlier
phases finish before the next phase reads their results.

```
OnUpdate:
  state.Dependency.Complete()                ? block on last frame's jobs

  CacheCalculationsFromLastFrame()           ? write Temp* job results into the caches

  if RebuildGraph(): return                  ? if graph rebuild is in flight, pause pathing

  Level.IsInitialised.Value = true

  ProcessPathRequests()                      ? drain PathRequests ? schedule FindPathsJob
  ProcessFlowRequests()                      ? drain FlowRequests ? schedule FindFlowsJob
  ProcessLineRequests()                      ? drain LineRequests ? schedule FindSightlinesJob

  FindNewRequests():                         ? four chained IJobEntity (single-thread) jobs
      InvalidatePathsJob                     ? evict paths flagged InvalidPathData
      RequestPathsJob                        ? MissingPathData ? enqueue PathRequest
      RequestFlowsJob                        ? MissingFlowData ? enqueue FlowRequest
      RequestSightlinesJob                   ? MissingSightlineData ? enqueue LineRequest

  FollowPaths():                             ? parallel IJobEntity
      FollowPathsJob                         ? per-agent: pick direction, or tag missing data
      DebugPathsJob                          ? editor only
```

### 5.1 Graph rebuild (`RebuildGraph` / `ScheduleSectorRebuild` / `CompleteSectorRebuild`)
- Walks `Level.RebuildFlags[i]` and queues at most `MAX_REBUILDS_PER_FRAME` (= 8) sectors.
- For each queued sector: `FlowCache.ClearSector(i)` then `Graph.InstantiateSector(i)` (a
  fresh `GraphSector` is allocated and filled by `RebuildGraphJob` in parallel).
- When all queued sectors have been rebuilt this frame, the new sectors replace the old
  ones (`Graph.StoreSector`). When the *whole* dirty list is empty,
  `Graph.GraphVersion.Value++` and `RecalculateContinentsJob` runs.
- While a rebuild is in flight, the rest of `OnUpdate` is skipped (pathing is paused for
  one frame).

### 5.2 Path computation
1. **Agent side** (`FollowPathsJob`):
   - If both endpoints are in different continents ? `PathIsImpossible = true`, done.
   - Else build a deterministic `pathKey = (start, dest, levelSize, travelType)`.
   - If `PathCache` doesn't contain `pathKey` ? add `MissingPathData{Start, Dest, �}`,
     return.
   - Else if cached path is `IsPending` ? return (wait).
   - Else walk the path: locate the current `PortalPathNode` in the agent's sector, look
     up the `FlowField` for that node (`flowKey = node.FlowCacheKey`); if missing ?
     add `MissingFlowData`, return. Otherwise sample `FlowField.Directions` to get the
     direction vector.
   - If `PathSmoothingMode.LineOfSight`, look ahead a few path nodes and prefer a direct
     sightline (cached via `LineCache`).

2. **Request gathering** (`RequestPathsJob`, after `FollowPathsJob`):
   - For each entity tagged `MissingPathData`: if not yet in `PathCache`, `PathRequests
     .Enqueue(...)`. The tag is removed in either case.

3. **Path scheduling** (`ProcessPathRequests`, top of *next* frame):
   - Decide how many path tasks to run this frame (`MAX_PATHFINDS_PER_FRAME` = 32).
   - For each: dequeue a request, ensure the *start and dest flow tiles* are already
     cached (else enqueue `FlowRequest`s and skip this path for now), then build a
     `FindPathsJob.Task` and mark the path as `IsPending` in `PathCache`.
   - Schedule `FindPathsJob.ScheduleParallel(count, 1, dependency)`.
   - Each task: `PortalPathfinder.TryFindPath(start, startField, dest, destField, �)`
     does an A\* over the portal graph using flow distances at the endpoints, then writes
     the resulting `UnsafeList<PortalPathNode>` and a `Success` flag into the task.

4. **Result commit** (`CacheCalculationsFromLastFrame`, top of frame after that):
   - Each completed `FindPathsJob.Task` is written into `PathCache` (now non-pending).

### 5.3 Flow computation
Mirrors the path pipeline. `ProcessFlowRequests` builds a `FindFlowsJob.Task` per request,
each running `FlowCalculator.Calculate` (Dijkstra wavefront from the goal bounds across
the sector's cost grid). Result is stored in `FlowCache` keyed by
`(goalCell, direction, travelType)`.

### 5.4 Sightline computation
`FindSightlinesJob` rasterises a line between two cells against the graph's cost data and
records "line is unblocked" / "line is blocked" in `LineCache`. Used only by
`PathSmoothingMode.LineOfSight` agents to round corners off chunky portal paths.

---

## 6. Multi-frame "trickle" pattern

Several places intentionally cap how much work runs per frame:

| Stage | Cap | Where | Actually enforced? |
| --- | --- | --- | --- |
| Sector rebuild | `MAX_REBUILDS_PER_FRAME` (8) | `ScheduleSectorRebuild` | **yes** |
| Path A\* tasks | `MAX_PATHFINDS_PER_FRAME` (32) | `ProcessPathRequests` | **yes** (loop bound + `PathRequests.Clear()` on overflow) |
| Flow tile builds | `MAX_FLOWFIELDS_PER_FRAME` (16) | `ProcessFlowRequests` | **no** � only used as the initial `NativeList` capacity; the loop runs over `numRequests`, so all queued flows are scheduled in one frame |
| Sightline checks | `MAX_SIGHTLINES_PER_FRAME` (128) | `ProcessLineRequests` | **no** � same as flows |
| LOS lookahead per agent | `MAX_LINE_OF_SIGHT_LOOKAHEAD` (5) | `FollowPathsJob` | yes |

Agent recovery loop: `RequestPathsJob` *unconditionally* removes the `MissingPathData`
tag (whether it enqueued a request or not). On the next frame, if the path still isn't
cached, `FollowPathsJob` re-adds the tag. So unserviced agents drive their own retry by
re-emitting the tag every frame until a placeholder or a real path appears in
`PathCache`. This is the deferral mechanism that lets thousands of agents share a fixed
compute budget � at the cost of recomputing `RequestPathsJob` work for every unsatisfied
agent every frame.

---

## 7. Quirks / known oddities to be aware of

- **`MAX_FLOWFIELDS_PER_FRAME` / `MAX_SIGHTLINES_PER_FRAME` are advisory, not enforced.**
  See the table above. If you actually need flow/sightline throttling, the loop bound has
  to change from `numRequests` to `numTasks` in `ProcessFlowRequests` /
  `ProcessLineRequests`, and the leftover requests must be left in the queue (today they
  are all dequeued).
- **`ProcessPathRequests` discards overflow with `PathRequests.Clear()`.** This is
  recoverable because `FollowPathsJob` re-emits `MissingPathData` for any unsatisfied
  agent next frame, but it (a) wastes work in `RequestPathsJob` every frame, and
  (b) destroys queue order so FIFO fairness across frames is lost.
- **`PathCache.WaitForCapacity` `break` also discards the request that was just
  dequeued.** Same recovery via re-emission, but the dropped request is lost from this
  frame's queue and never re-enqueued explicitly.
- **Flow-cache miss in `ProcessPathRequests` `continue`s without storing a placeholder**,
  so the path request is dropped and the agent recovers via re-emission next frame. One
  frame of latency per cold-start path.
- **`FlowCache` has no eviction**, so over a long session the hashmap keeps growing as
  new goal cells appear. Sectors being rebuilt is the only thing that removes entries.
- **`FlowCalculator.Calculate` throws** if `GoalBounds` doesn't intersect the sector
  bounds (line 81�83 of `FlowCalculator.cs`). Since this runs inside a Burst job,
  hitting it crashes the worker. It can be triggered if a stale `FlowRequest` outlives
  the sector geometry it was built against.
- **`ContinentPathfinder` has hard-coded iteration caps of 9999** in two `while (true)`
  loops. Levels with more than 9999 portals or more than 9999 connected components could
  silently terminate continent labelling early.

## 8. Determinism, versioning, and invalidation

- Every `SectorData` carries a monotonically-increasing `Version`. Every `PortalPathNode`
  and `FlowField` records the version of the sector it was built from.
- `FollowPathsJob` checks the version of the next 1�3 nodes in its path; a mismatch
  invalidates the path (`InvalidPathData` tag ? `InvalidatePathsJob` evicts it).
- Empty (`!PathWasFound`) results are kept in cache and only re-tried after
  `Graph.GraphVersion` ticks, so impossible paths don't get retried every frame.
- `FlowCache.ClearSector` is called whenever a sector rebuilds, so every flow field
  belonging to that sector is dropped.

---

## 9. File map

```
Plugins/FlowTiles/
??? Level/
?   ??? PathableGrid.cs        ? raw cost grid + dirty flags
?   ??? CostStamp.cs           ? bulk grid edits
?   ??? SectorFlags.cs         ? per-sector "needs rebuild" flag
??? PortalPaths/
?   ??? ContinentPathfinder.cs ? BFS that labels portals with continent ids
?   ??? PortalPathfinder.cs    ? HPA* over the portal graph
?   ??? SectorPathfinder.cs    ? A* inside one sector (used to build internal edges)
?   ??? PortalPathNode.cs      ? one step of an HPA path
?   ??? PortalGraph/           ? PathableGraph + GraphSector + Sector* data
??? FlowFields/
?   ??? FlowField.cs           ? per-(sector,goal) direction grid
?   ??? FlowCalculator.cs      ? Dijkstra wavefront builder
??? ECS/
?   ??? Systems/PathfindingSystem.cs   ? the frame loop
?   ??? Components/                    ? FlowGoal, FlowPosition, FlowProgress, Missing* tags
?   ??? Caches/                        ? PathCache, FlowCache, LineCache + key types
?   ??? Jobs/
?       ??? FollowPathsJob.cs          ? per-agent direction picker
?       ??? RequestPathsJob.cs         ? MissingPathData ? PathRequests
?       ??? RequestFlowsJob.cs         ? MissingFlowData ? FlowRequests
?       ??? RequestSightlinesJob.cs    ? MissingSightlineData ? LineRequests
?       ??? InvalidatePathsJob.cs      ? InvalidPathData ? evict from cache
?       ??? FindPathsJob.cs            ? parallel HPA* tasks
?       ??? FindFlowsJob.cs            ? parallel flow-field builds
?       ??? FindSightlinesJob.cs       ? parallel sightline checks
?       ??? RebuildGraphJob.cs         ? parallel sector rebuilds
?       ??? RecalculateContinentsJob.cs? single-thread continent labelling
??? Utils/                     ? native collections, math helpers, constants
```
