using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Firefly
{
    sealed class ParticleExpirationBarrier : BarrierSystem {}

    sealed class ParticleExpirationSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct ParticleExpirationJob : IJob
        {
            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public ComponentDataArray<Particle> Particles;

            public EntityCommandBuffer CommandBuffer;

            public void Execute()
            {
                for (var i = 0; i < Entities.Length; i++)
                {
                    if (Particles[i].Life > 3 + Particles[i].Random * 5)
                        CommandBuffer.DestroyEntity(Entities[i]);
                }
            }
        }

        struct Group
        {
            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public ComponentDataArray<Particle> Particles;
        }

        [Inject] ParticleExpirationBarrier _barrier;
        [Inject] Group _group;

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            var job = new ParticleExpirationJob() {
                Entities = _group.Entities,
                Particles = _group.Particles,
                CommandBuffer = _barrier.CreateCommandBuffer()
            };
            return job.Schedule(deps);
        }
    }
}
