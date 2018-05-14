using Unity.Entities;
using UnityEngine;

[System.Serializable]
struct FlySpawner : ISharedComponentData
{
    public Mesh templateMesh;
}

class FlySpawnerComponent : SharedComponentDataWrapper<FlySpawner> {}
