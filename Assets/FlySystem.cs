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

    public UnityEngine.Mesh sharedMesh {
        get { return _mesh; }
    }

    const int kMaxVertices = 60000;

    ComponentGroup _group;

    UnityEngine.Mesh _mesh;

    NativeArray<float3> _vertexCache;
    NativeArray<float3> _normalCache;

    UnityEngine.Vector3[] _managedVertexArray;
    UnityEngine.Vector3[] _managedNormalArray;

    protected override void OnCreateManager(int capacity)
    {
        _group = GetComponentGroup(typeof(Fly), typeof(Facet), typeof(Position));

        _mesh = new UnityEngine.Mesh();

        _vertexCache = new NativeArray<float3>(kMaxVertices, Allocator.Persistent);
        _normalCache = new NativeArray<float3>(kMaxVertices, Allocator.Persistent);

        _managedVertexArray = new UnityEngine.Vector3[kMaxVertices];
        _managedNormalArray = new UnityEngine.Vector3[kMaxVertices];

        _mesh.vertices = _managedVertexArray;
        _mesh.normals = _managedNormalArray;

        var indices = new int[_vertexCache.Length];
        for (var i = 0; i < kMaxVertices; i++) indices[i] = i;
        _mesh.triangles = indices;
    }

    protected override void OnDestroyManager()
    {
        UnityEngine.Object.Destroy(_mesh);

        _vertexCache.Dispose();
        _normalCache.Dispose();

        _managedVertexArray = null;
        _managedNormalArray = null;
    }

    unsafe protected override JobHandle OnUpdate(JobHandle deps)
    {
        var copySize = sizeof(float3) * kMaxVertices;

        var pVArray = UnsafeUtility.AddressOf(ref _managedVertexArray[0]);
        var pNArray = UnsafeUtility.AddressOf(ref _managedNormalArray[0]);

        UnsafeUtility.MemCpy(pVArray, _vertexCache.GetUnsafePtr(), copySize);
        UnsafeUtility.MemCpy(pNArray, _normalCache.GetUnsafePtr(), copySize);

        _mesh.vertices = _managedVertexArray;
        _mesh.normals = _managedNormalArray;

        var flies = _group.GetComponentDataArray<Fly>();

        var constructionJob = new ConstructionJob() {
            Flies = flies,
            Facets = _group.GetComponentDataArray<Facet>(),
            Positions = _group.GetComponentDataArray<Position>(),
            Vertices = _vertexCache,
            Normals = _normalCache
        };

        return constructionJob.Schedule(flies.Length, 64, deps);
    }
}
