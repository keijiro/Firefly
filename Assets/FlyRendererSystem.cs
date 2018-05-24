using Unity.Entities;
using System.Collections.Generic;

/*
public class FlyRendererSystem : ComponentSystem
{
    List<FlyRenderer> _rendererDatas = new List<FlyRenderer>();

    protected override void OnCreateManager(int capacity)
    {
    }

    protected override void OnUpdate()
    {
        EntityManager.GetAllUniqueSharedComponentDatas(_rendererDatas);

        var matrix = UnityEngine.Matrix4x4.identity;

        foreach (var data in _rendererDatas)
        {
            if (data.material == null || data.mesh == null) continue;
            UnityEngine.Graphics.DrawMesh(data.mesh, matrix, data.material, 0);
        }

        _rendererDatas.Clear();
    }
}
*/
