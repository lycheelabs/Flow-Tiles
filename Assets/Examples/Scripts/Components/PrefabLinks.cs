using Unity.Entities;

namespace LycheeLabs.FlowTiles.Examples {

    public struct PrefabLinks : IComponentData {

        public Entity Wall;
        public Entity Agent;
        public Entity Flow;

    }

}