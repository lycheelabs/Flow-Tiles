using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace LycheeLabs.FlowTiles.Examples {

    public class WallAuthor : MonoBehaviour {

        public class MyBaker : Baker<WallAuthor> {
            public override void Bake(WallAuthor authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WallData { });
                AddComponent(entity, new ColorOverride { Value = new float4(1) });
            }
        }

    }

}