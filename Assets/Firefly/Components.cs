using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Firefly
{
    #region Basic data structure

    struct Triangle : IComponentData
    {
        public float3 Vertex1;
        public float3 Vertex2;
        public float3 Vertex3;
    }

    #endregion

    #region Instancing and rendering

    [System.Serializable]
    struct Instance : ISharedComponentData
    {
        public UnityEngine.Mesh TemplateMesh;
    }

    [System.Serializable]
    struct RenderSettings : ISharedComponentData
    {
        public UnityEngine.Material Material;
        public UnityEngine.Rendering.ShadowCastingMode CastShadows;
        public bool ReceiveShadows;
    }

    struct Renderer : ISharedComponentData
    {
        public const int MaxVertices = 510000;
        public RenderSettings Settings;
        public UnityEngine.Mesh WorkMesh;
        public UnityEngine.Vector3[] Vertices;
        public UnityEngine.Vector3[] Normals;
        public NativeCounter Counter;
        public NativeCounter.Concurrent ConcurrentCounter;
    }

    #endregion

    #region Particle system base

    struct Particle : IComponentData
    {
        public float3 Velocity;
        public uint ID;
        public float LifeRandom;
        public float Time;
    }

    interface IParticleVariant
    {
        float GetWeight();
        float GetLife();
    }

    #endregion

    #region Particle system variants

    [System.Serializable]
    struct SimpleParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }

        public float Life;
        public float GetLife() { return Life; }
    }

    [System.Serializable]
    struct ButterflyParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }

        public float Life;
        public float GetLife() { return Life; }
    }

    #endregion

    #region Particle behavior effector

    [System.Serializable]
    struct NoiseEffector : IComponentData
    {
        public float Frequency;
        public float Amplitude;
    }

    #endregion
}
