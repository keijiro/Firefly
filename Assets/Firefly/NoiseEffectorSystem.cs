using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

namespace Firefly
{
    sealed class NoiseEffectorSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct AnimationJob : IJobProcessComponentData<Particle, Position>
        {
            public NoiseEffector Effector;
            public float4x4 Transform;
            public float DeltaTime;

            float3 DFNoise(float3 p)
            {
                p *= Effector.Frequency;

                float3 grad1;
                noise.snoise(p, out grad1);

                p.z += 100;

                float3 grad2;
                noise.snoise(p, out grad2);

                return math.cross(grad1, grad2);
            }

            float Amplitude(float3 p)
            {
                var z = math.mul(Transform, new float4(p, 1)).z;
                return math.saturate(z + 0.5f);
            }

            public void Execute(ref Particle particle, ref Position position)
            {
                var pos = position.Value;
                var acc = DFNoise(pos) * Effector.Amplitude;
                var dt = DeltaTime * Amplitude(pos);

                particle.Velocity += acc * dt;
                particle.Time += dt;

                position.Value += particle.Velocity * dt;
            }
        }

        struct Group
        {
            [ReadOnly] public ComponentDataArray<NoiseEffector> Effectors;
            [ReadOnly] public ComponentArray<UnityEngine.Transform> Transforms;
            public int Length;
        }

        [Inject] Group _group;

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            for (var i = 0; i < _group.Length; i++)
            {
                var job = new AnimationJob() {
                    Effector = _group.Effectors[i],
                    Transform = _group.Transforms[i].worldToLocalMatrix,
                    DeltaTime = UnityEngine.Time.deltaTime
                };
                deps = job.Schedule(this, 32, deps);
            }
            return deps;
        }
    }
}
