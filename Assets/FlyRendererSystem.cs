using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;

public class FlyRendererSystem : ComponentSystem
{
    List<FlyRenderer> _renderers = new List<FlyRenderer>();
    ComponentGroup _dependency; // used just for dependency tracking

    UnityEngine.Vector3[] _managedVertexArray;
    UnityEngine.Vector3[] _managedNormalArray;
    int[] _managedIndexArray;

    protected override void OnCreateManager(int capacity)
    {
        _dependency = GetComponentGroup(typeof(Fly), typeof(FlyRenderer));

        _managedVertexArray = new UnityEngine.Vector3[FlyRenderer.kMaxVertices];
        _managedNormalArray = new UnityEngine.Vector3[FlyRenderer.kMaxVertices];
        _managedIndexArray = new int[FlyRenderer.kMaxVertices];

        for (var i = 0; i < FlyRenderer.kMaxVertices; i++) _managedIndexArray[i] = i;
    }

    protected override void OnDestroyManager()
    {
        _managedVertexArray = null;
        _managedNormalArray = null;
        _managedIndexArray = null;
    }

    unsafe protected override void OnUpdate()
    {
        EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

        var matrix = UnityEngine.Matrix4x4.identity;
        var copySize = sizeof(float3) * FlyRenderer.kMaxVertices;

        var pVArray = UnsafeUtility.AddressOf(ref _managedVertexArray[0]);
        var pNArray = UnsafeUtility.AddressOf(ref _managedNormalArray[0]);

        foreach (var renderer in _renderers)
        {
            if (renderer.MeshInstance == null) continue;

            UnsafeUtility.MemCpy(pVArray, renderer.Vertices.GetUnsafePtr(), copySize);
            UnsafeUtility.MemCpy(pNArray, renderer.Normals.GetUnsafePtr(), copySize);

            renderer.MeshInstance.vertices = _managedVertexArray;
            renderer.MeshInstance.normals = _managedNormalArray;
            renderer.MeshInstance.triangles = _managedIndexArray;

            UnityEngine.Graphics.DrawMesh(
                renderer.MeshInstance, matrix, renderer.Settings.material, 0
            );
        }

        _renderers.Clear();
    }
}
