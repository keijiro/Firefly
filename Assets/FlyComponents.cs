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
