using Unity.Entities;
using UnityEngine;

namespace FlowTiles.Examples {
    public class AgentAuthor : MonoBehaviour {

        public class MyBaker : Baker<AgentAuthor> {
            public override void Bake(AgentAuthor authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AgentData { });
                AddComponent(entity, new PathfindingData { });
            }
        }

    }

}