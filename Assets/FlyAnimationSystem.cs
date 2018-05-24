using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

public class FlyAnimationSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct ConstructionJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Fly> Flies;
        [ReadOnly] public ComponentDataArray<Facet> Facets;
        [ReadOnly] public ComponentDataArray<Position> Positions;

        [NativeDisableParallelForRestriction] public NativeArray<float3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Normals;

        public void Execute(int index)
        {
            var p = Positions[index].Value;
            var f = Facets[index];
            var vi = index * 3;

            var v1 = p + f.Vertex1;
            var v2 = p + f.Vertex2;
            var v3 = p + f.Vertex3;
            var n = math.normalize(math.cross(v2 - v1, v3 - v1));

            Vertices[vi + 0] = v1;
            Vertices[vi + 1] = v2;
            Vertices[vi + 2] = v3;

            Normals[vi + 0] = n;
            Normals[vi + 1] = n;
            Normals[vi + 2] = n;
        }
    }

    class MeshCache
    {
        public NativeArray<float3> Vertices;
        public NativeArray<float3> Normals;
        public FlyRenderer RendererSettings;
        public UnityEngine.Mesh MeshInstance;

        public MeshCache()
        {
            Vertices = new NativeArray<float3>(kMaxVertices, Allocator.Persistent);
            Normals = new NativeArray<float3>(kMaxVertices, Allocator.Persistent);
            MeshInstance = new UnityEngine.Mesh();
        }

        public void Release()
        {
            Vertices.Dispose();
            Normals.Dispose();
            UnityEngine.Object.Destroy(MeshInstance);
        }
    }

    List<MeshCache> _meshCaches = new List<MeshCache>();

    List<FlyRenderer> _rendererDatas = new List<FlyRenderer>();
    ComponentGroup _flyGroup;

    const int kMaxVertices = 60000;

    UnityEngine.Vector3[] _managedVertexArray;
    UnityEngine.Vector3[] _managedNormalArray;
    int[] _managedIndexArray;

    protected override void OnCreateManager(int capacity)
    {
        _flyGroup = GetComponentGroup(
            typeof(Fly), typeof(Facet), typeof(Position), typeof(FlyRenderer)
        );

        _managedVertexArray = new UnityEngine.Vector3[kMaxVertices];
        _managedNormalArray = new UnityEngine.Vector3[kMaxVertices];
        _managedIndexArray = new int[kMaxVertices];

        for (var i = 0; i < kMaxVertices; i++) _managedIndexArray[i] = i;
    }

    protected override void OnDestroyManager()
    {
        foreach (var mc in _meshCaches) mc.Release();
        _meshCaches.Clear();

        _managedVertexArray = null;
        _managedNormalArray = null;
        _managedIndexArray = null;
    }

    unsafe protected override JobHandle OnUpdate(JobHandle deps)
    {
        var matrix = UnityEngine.Matrix4x4.identity;

        foreach (var cache in _meshCaches)
        {
            var copySize = sizeof(float3) * kMaxVertices;

            var pVArray = UnsafeUtility.AddressOf(ref _managedVertexArray[0]);
            var pNArray = UnsafeUtility.AddressOf(ref _managedNormalArray[0]);

            UnsafeUtility.MemCpy(pVArray, cache.Vertices.GetUnsafePtr(), copySize);
            UnsafeUtility.MemCpy(pNArray, cache.Normals.GetUnsafePtr(), copySize);

            cache.MeshInstance.vertices = _managedVertexArray;
            cache.MeshInstance.normals = _managedNormalArray;
            cache.MeshInstance.triangles = _managedIndexArray;

            UnityEngine.Graphics.DrawMesh(
                cache.MeshInstance, matrix, cache.RendererSettings.material, 0
            );
        }

        EntityManager.GetAllUniqueSharedComponentDatas(_rendererDatas);

        var cacheCount = 0;
        for (var i = 0; i < _rendererDatas.Count; i++)
        {
            if (_rendererDatas[i].material == null) continue;

            if (cacheCount >= _meshCaches.Count) _meshCaches.Add(new MeshCache());
            var cache = _meshCaches[cacheCount++];
            cache.RendererSettings = _rendererDatas[i];

            _flyGroup.SetFilter(_rendererDatas[i]);

            var job = new ConstructionJob() {
                Flies = _flyGroup.GetComponentDataArray<Fly>(),
                Facets = _flyGroup.GetComponentDataArray<Facet>(),
                Positions = _flyGroup.GetComponentDataArray<Position>(),
                Vertices = cache.Vertices,
                Normals = cache.Normals
            };

            deps = job.Schedule(_flyGroup.CalculateLength(), 64, deps);
        }

        while (cacheCount > _meshCaches.Count)
        {
            var i = _meshCaches.Count - 1;
            _meshCaches[i].Release();
            _meshCaches.RemoveAt(i);
        }

        _rendererDatas.Clear();

        return deps;
    }
}
