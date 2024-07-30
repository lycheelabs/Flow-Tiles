using Unity.Entities;
using UnityEngine;

namespace FlowTiles.Examples {
    public class FlowAuthor : MonoBehaviour {

        public class MyBaker : Baker<FlowAuthor> {
            public override void Bake(FlowAuthor authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new FlowData { });
            }
        }

    }

}