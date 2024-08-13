# Flow Tiles Pathfinding (Unity DOTS 2024)

This package is an implementation of the 'Flow Field Tiles' algorithm first detailed in this paper:
[Crowd Pathfinding and Steering Using Flow Field Tiles.pdf](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf)

### I have borrowed and built upon code from other developers.  
- The HPA pathfinder was originally based on [this implementation](https://github.com/hugoscurti/hierarchical-pathfinding)
- The NativePriotityQueue collection uses [this implementation](https://gist.github.com/StagPoint/02a845585f6900a48e9035b00f07726e)
- The NativeStack collection uses [this implementation](https://github.com/jacksondunstan/NativeCollections)

### This software is provided under the MIT license, without warranty.
- You are free to use this software as you please, personally and commercially.
- If you republish this software edited or unedited, post it under the MIT license.
- I am not responsible for anything bad that happens from using this software.
- Read license.md for more information

### The Flow Tiles Algorithm
This algorithm combines the benefits of Hierarchical A* Pathfinding (HPA) with Flow Field pathfinding. 
- The world is subdivided into sectors, which contain exits connecting them to neighbor sectors.
- These exits are connected into an HPA graph. Paths through this graph can be found quickly as the graph is coarse.
- To follow the found path, small flow fields are generated lazily for each sector exit as the path is followed.
- Paths can be cached and flow fields can also be cached, making this very fast when many agents follow a 'similar' path (through the same sector exits).
- The level can be modified, triggering a rebuild of the relevant sectors, paths and flows.
- It is possible for different agent types to find different paths (like ground-based and amphibious paths). However each new agent type added is computationally expensive!
- This implementation is fully DOTS compatible, making use of entities, jobs and burst to achieve high numbers of agents.

### Algorithm Weaknesses
- This algorithm does not provide perfect paths. There are multiple sources of sub-optimality, resulting in artefacts like zig-zagging. This algorithm should be used when scale is more important than quality.
- This algorithm works best when agents are pathing to a small number of destination points, such as a building in the middle of a base.
- This algorithm does support modifying and rebuilding the level, but works best when this is infrequent (with many frames between each edit).

### Example Scenes
- **Graph example:** Demonstrates the building and rebuilding of the HPA graph, with visualisation modes available.
- **Agent example:** Demonstrates an agent finding and following a path.
- **Terrain example:** One agent here prefers ground terrain, while the other prefers water terrain.
- **Stress Test example:** This example shows huge numbers of agents pathing to a range of destination points.
- **Changing Stress Test example:** This example also has moving walls, triggering regular rebuilds of the pathing data.

### Help - I opened an example scene but nothing is visible!
- Depending on your version of DOTS, you may need to reimport the ECS subscene or perhaps even clear your entities cache. DOTS is still very finicky, especially when subscenes are involved.