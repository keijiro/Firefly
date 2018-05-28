using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Firefly
{
    public struct Triangle : IComponentData
    {
        public float3 Vertex1;
        public float3 Vertex2;
        public float3 Vertex3;
    }

    public struct Particle : IComponentData
    {
        public float3 Velocity;
        public float Life;
    }

    public struct Renderer : ISharedComponentData
    {
        public const int MaxVertices = 510000;
        public RenderSettings Settings;
        public UnityEngine.Mesh WorkMesh;
        public NativeArray<float3> Vertices;
        public NativeArray<float3> Normals;
        public NativeCounter Counter;
    }
}
