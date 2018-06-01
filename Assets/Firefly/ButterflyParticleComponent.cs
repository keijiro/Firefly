using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct ButterflyParticle : ISharedComponentData, IParticleVariant
    {
        public float Weight;
        public float GetWeight() { return Weight; }
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Butterfly Particle")]
    class ButterflyParticleComponent : SharedComponentDataWrapper<ButterflyParticle> {}
}
