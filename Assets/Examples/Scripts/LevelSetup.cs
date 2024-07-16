using Unity.Collections;
using Unity.Entities;

namespace FlowTiles.Examples {
    public struct LevelSetup : IComponentData {

        public int Size;
        public NativeArray<bool> Walls;

    }

}