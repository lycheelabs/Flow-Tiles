using FlowTiles.Examples;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class DemoManager : MonoBehaviour
{

    public int LevelSize = 1000;

    void Start() {
        var halfViewedSize = (LevelSize - 1) / 2f;
        Camera.main.orthographicSize = LevelSize / 2f * 1.05f + 1;
        Camera.main.transform.position = new Vector3(halfViewedSize, halfViewedSize, -20);
        
        var levelSetup = new LevelSetup {
            Size = LevelSize,
        };

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var singleton = em.CreateEntity();
        em.AddComponent<LevelSetup>(singleton);
        em.SetComponentData(singleton, levelSetup);
    }

    void Update() {
        //
    }
}
