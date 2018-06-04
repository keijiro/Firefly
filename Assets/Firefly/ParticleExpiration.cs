using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using System.Collections.Generic;

namespace Firefly
{
    sealed class ParticleExpirationBarrier : BarrierSystem {}

    [ComputeJobOptimization]
    struct ParticleExpirationJob : IJob
    {
        [ReadOnly] public EntityArray Entities;
        [ReadOnly] public ComponentDataArray<Particle> Particles;

        public float Life;
        public EntityCommandBuffer CommandBuffer;

        public void Execute()
        {
            for (var i = 0; i < Entities.Length; i++)
            {
                var life = Life * (Particles[i].Random + 1) / 2;
                if (Particles[i].Time > life)
                    CommandBuffer.DestroyEntity(Entities[i]);
            }
        }
    }

    class ParticleExpirationSystemBase<T> : JobComponentSystem
        where T : struct, ISharedComponentData, IParticleVariant
    {
        [Inject] protected ParticleExpirationBarrier _barrier;

        List<T> _variants = new List<T>();

        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(typeof(Particle), typeof(T));
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            var commandBuffer = _barrier.CreateCommandBuffer();

            EntityManager.GetAllUniqueSharedComponentDatas(_variants);

            foreach (var variant in _variants)
            {
                _group.SetFilter(variant);

                var job = new ParticleExpirationJob() {
                    Entities = _group.GetEntityArray(),
                    Particles = _group.GetComponentDataArray<Particle>(),
                    Life = variant.GetLife(),
                    CommandBuffer = commandBuffer
                };
                deps = job.Schedule(deps);
            }

            _variants.Clear();

            return deps;
        }
    }
}
