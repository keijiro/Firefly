using Unity.Entities;

[System.Serializable]
struct FlySpawn : ISharedComponentData
{
    public UnityEngine.Mesh templateMesh;
}

class FlySpawnComponent : SharedComponentDataWrapper<FlySpawn> {}
