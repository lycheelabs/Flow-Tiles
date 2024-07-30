using Unity.Entities;
using UnityEngine;

namespace FlowTiles.Examples {

    public class PrefabsAuthor : MonoBehaviour {

        public GameObject WallPrefab;
        public GameObject AgentPrefab;
        public GameObject FlowPrefab;

        public class MyBaker : Baker<PrefabsAuthor> {
            public override void Bake(PrefabsAuthor authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PrefabLinks {
                    Wall = GetEntity(authoring.WallPrefab, TransformUsageFlags.None),
                    Agent = GetEntity(authoring.AgentPrefab, TransformUsageFlags.Dynamic),
                    Flow = GetEntity(authoring.FlowPrefab, TransformUsageFlags.Dynamic),
                });
            }
        }

    }

}