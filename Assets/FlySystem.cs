using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

public class FlySystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct ConstructionJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Fly> Flies;
        [ReadOnly] public ComponentDataArray<Position> Positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<float3> Vertices;

        public void Execute(int index)
        {
            var vi = index * 3;
            var p = Positions[index].Value;
            Vertices[vi + 0] = p;
            Vertices[vi + 1] = p + new float3(0.0f, 0.1f, 0.0f);
            Vertices[vi + 2] = p + new float3(0.1f, 0.0f, 0.0f);
        }
    }

    public UnityEngine.Mesh sharedMesh {
        get { return _mesh; }
    }

    ComponentGroup _group;
    UnityEngine.Mesh _mesh;
    NativeArray<float3> _vertexCache;
    UnityEngine.Vector3[] _managedVertexArray;

    protected override void OnCreateManager(int capacity)
    {
        _group = GetComponentGroup(typeof(Fly), typeof(Position));
        _mesh = new UnityEngine.Mesh();
        _vertexCache = new NativeArray<float3>(60000, Allocator.Persistent);
        _managedVertexArray = new UnityEngine.Vector3[_vertexCache.Length];

        var indices = new int[_vertexCache.Length];
        for (var i = 0; i < indices.Length; i++) indices[i] = i;
        _mesh.vertices = _managedVertexArray;
        _mesh.SetTriangles(indices, 0);
    }

    protected override void OnDestroyManager()
    {
        UnityEngine.Object.Destroy(_mesh);
        _vertexCache.Dispose();
        _managedVertexArray = null;
    }

    unsafe protected override JobHandle OnUpdate(JobHandle deps)
    {
        UnsafeUtility.MemCpy(
            UnsafeUtility.AddressOf(ref _managedVertexArray[0]),
            _vertexCache.GetUnsafePtr(),
            sizeof(UnityEngine.Vector3) * _managedVertexArray.Length
        );

        _mesh.vertices = _managedVertexArray;

        var flies = _group.GetComponentDataArray<Fly>();

        var constructionJob = new ConstructionJob() {
            Flies = flies,
            Positions = _group.GetComponentDataArray<Position>(),
            Vertices = _vertexCache
        };

        return constructionJob.Schedule(flies.Length, 64, deps);
    }
}
