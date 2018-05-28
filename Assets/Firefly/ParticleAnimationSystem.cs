using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

namespace Firefly
{
    [UpdateBefore(typeof(ParticleReconstructionSystem))]
    class ParticleAnimationSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct AnimationJob : IJobProcessComponentData<Particle, Position>
        {
            public float Time;
            public float DeltaTime;

            public void Execute(
                [ReadOnly] ref Particle particle,
                ref Position position
            )
            {
                var np = position.Value * 6;

                float3 grad1, grad2;
                noise.snoise(np, out grad1);
                noise.snoise(np + 100, out grad2);

                var acc = math.cross(grad1, grad2) * 0.02f;

                var dt = DeltaTime * math.saturate(Time - 2 + position.Value.y * 2);

                position.Value += particle.Velocity * dt;
                particle.Life += dt;
                particle.Velocity += acc * dt;
            }
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
           var job = new AnimationJob() {
               Time = UnityEngine.Time.time,
               DeltaTime = UnityEngine.Time.deltaTime
           };
           return job.Schedule(this, 32, deps);
        }
    }
}
