using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct Fly : IComponentData
{
    public float Life;
}

public struct Facet : IComponentData
{
    public float3 Vertex1;
    public float3 Vertex2;
    public float3 Vertex3;
}

public struct SharedGeometryData : ISharedComponentData
{
    public const int kMaxVertices = 60000;
    public NativeArray<float3> Vertices;
    public NativeArray<float3> Normals;
    public UnityEngine.Mesh MeshInstance;
}
