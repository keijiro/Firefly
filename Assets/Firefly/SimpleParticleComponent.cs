using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct SimpleParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Simple Particle")]
    class SimpleParticleComponent : SharedComponentDataWrapper<SimpleParticle> {}
}
