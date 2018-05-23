using Unity.Entities;

[System.Serializable]
struct FlySpawner : ISharedComponentData
{
    public UnityEngine.Mesh templateMesh;
}

class FlySpawnerComponent : SharedComponentDataWrapper<FlySpawner> {}
