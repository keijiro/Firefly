using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct ButterflyParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }

        public float Life;
        public float GetLife() { return Life; }
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Butterfly Particle")]
    sealed class ButterflyParticleComponent : SharedComponentDataWrapper<ButterflyParticle> {}
}
