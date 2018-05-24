using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;

[UpdateBefore(typeof(FlyAnimationSystem))]
public class FlyRendererSystem : ComponentSystem
{
    List<FlyRenderer> _rendererDatas = new List<FlyRenderer>();
    ComponentGroup _rendererGroup;

    UnityEngine.Vector3[] _managedVertexArray;
    UnityEngine.Vector3[] _managedNormalArray;
    int[] _managedIndexArray;

    protected override void OnCreateManager(int capacity)
    {
        _rendererGroup = GetComponentGroup(
            typeof(FlyRenderer), typeof(SharedGeometryData)
        );

        _managedVertexArray = new UnityEngine.Vector3[SharedGeometryData.kMaxVertices];
        _managedNormalArray = new UnityEngine.Vector3[SharedGeometryData.kMaxVertices];
        _managedIndexArray = new int[SharedGeometryData.kMaxVertices];

        for (var i = 0; i < SharedGeometryData.kMaxVertices; i++) _managedIndexArray[i] = i;
    }

    protected override void OnDestroyManager()
    {
        _managedVertexArray = null;
        _managedNormalArray = null;
        _managedIndexArray = null;
    }

    unsafe protected override void OnUpdate()
    {
        EntityManager.GetAllUniqueSharedComponentDatas(_rendererDatas);

        var matrix = UnityEngine.Matrix4x4.identity;
        var copySize = sizeof(float3) * SharedGeometryData.kMaxVertices;

        var pVArray = UnsafeUtility.AddressOf(ref _managedVertexArray[0]);
        var pNArray = UnsafeUtility.AddressOf(ref _managedNormalArray[0]);

        foreach (var rendererData in _rendererDatas)
        {
            if (rendererData.material == null) continue;

            _rendererGroup.SetFilter(rendererData);

            var head = _rendererGroup.GetEntityArray()[0];
            var geometry = EntityManager.GetSharedComponentData<SharedGeometryData>(head);

            UnsafeUtility.MemCpy(pVArray, geometry.Vertices.GetUnsafePtr(), copySize);
            UnsafeUtility.MemCpy(pNArray, geometry.Normals.GetUnsafePtr(), copySize);

            geometry.MeshInstance.vertices = _managedVertexArray;
            geometry.MeshInstance.normals = _managedNormalArray;
            geometry.MeshInstance.triangles = _managedIndexArray;

            UnityEngine.Graphics.DrawMesh(
                geometry.MeshInstance, matrix, rendererData.material, 0
            );
        }

        _rendererDatas.Clear();
    }
}
