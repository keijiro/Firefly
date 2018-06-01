using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct SimpleParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }

        public float Life;
        public float GetLife() { return Life; }
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Simple Particle")]
    sealed class SimpleParticleComponent : SharedComponentDataWrapper<SimpleParticle> {}
}
