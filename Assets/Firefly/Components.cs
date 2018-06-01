using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Firefly
{
    struct Triangle : IComponentData
    {
        public float3 Vertex1;
        public float3 Vertex2;
        public float3 Vertex3;
    }

    struct Particle : IComponentData
    {
        public float3 Velocity;
        public float Time;
        public float Random;
    }

    interface IParticleVariant
    {
        float GetWeight();
        float GetLife();
    }

    struct Renderer : ISharedComponentData
    {
        public const int MaxVertices = 510000;
        public RenderSettings Settings;
        public UnityEngine.Mesh WorkMesh;
        public UnityEngine.Vector3 [] Vertices;
        public UnityEngine.Vector3 [] Normals;
        public NativeCounter Counter;
        public NativeCounter.Concurrent ConcurrentCounter;
    }
}
