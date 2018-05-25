using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Firefly
{
    public struct Facet : IComponentData
    {
        public float3 Vertex1;
        public float3 Vertex2;
        public float3 Vertex3;
    }

    public struct Renderer : ISharedComponentData
    {
        public const int kMaxVertices = 60000;
        public RenderSettings Settings;
        public UnityEngine.Mesh WorkMesh;
        public NativeArray<float3> Vertices;
        public NativeArray<float3> Normals;
        public NativeCounter Counter;
    }

    public struct Disintegrator : IComponentData
    {
        public float Life;
    }
}
