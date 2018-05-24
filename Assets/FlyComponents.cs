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

public struct FlyRenderer : ISharedComponentData
{
    public const int kMaxVertices = 60000;
    public FlyRenderSettings Settings;
    public NativeArray<float3> Vertices;
    public NativeArray<float3> Normals;
    public UnityEngine.Mesh MeshInstance;
}
