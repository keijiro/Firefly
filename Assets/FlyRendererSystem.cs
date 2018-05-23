using Unity.Entities;
using System.Collections.Generic;

public class FlyRendererSystem : ComponentSystem
{
    List<FlyRenderer> _sharedDataCache;

    [Inject] FlySystem _flySystem;

    FlyRenderer? GetSharedData()
    {
        _sharedDataCache.Clear();
        EntityManager.GetAllUniqueSharedComponentDatas(_sharedDataCache);
        foreach (var data in _sharedDataCache)
            if (data.material != null) return data;
        return null;
    }

    protected override void OnCreateManager(int capacity)
    {
        _sharedDataCache = new List<FlyRenderer>(10);
    }

    protected override void OnUpdate()
    {
        var sharedData = GetSharedData();
        if (sharedData == null) return;

        UnityEngine.Graphics.DrawMesh(
            _flySystem.sharedMesh, UnityEngine.Matrix4x4.identity,
            sharedData?.material, 0
        );
    }
}
