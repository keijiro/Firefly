using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    sealed class ButterflyParticleExpirationSystem
        : ParticleExpirationSystemBase<ButterflyParticle> {}

    sealed class ButterflyParticleSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        unsafe struct ReconstructionJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Particle> Particles;
            [ReadOnly] public ComponentDataArray<Position> Positions;
            [ReadOnly] public ComponentDataArray<Triangle> Triangles;

            [NativeDisableUnsafePtrRestriction] public void* Vertices;
            [NativeDisableUnsafePtrRestriction] public void* Normals;

            public NativeCounter.Concurrent Counter;

            void AddTriangle(float3 v1, float3 v2, float3 v3)
            {
                var i = Counter.Increment() * 3;

                UnsafeUtility.WriteArrayElement(Vertices, i + 0, v1);
                UnsafeUtility.WriteArrayElement(Vertices, i + 1, v2);
                UnsafeUtility.WriteArrayElement(Vertices, i + 2, v3);

                var n = math.normalize(math.cross(v2 - v1, v3 - v1));
                UnsafeUtility.WriteArrayElement(Normals, i + 0, n);
                UnsafeUtility.WriteArrayElement(Normals, i + 1, n);
                UnsafeUtility.WriteArrayElement(Normals, i + 2, n);
            }

            public void Execute(int index)
            {
                const float size = 0.005f;

                var p = Particles[index];

                var az = p.Velocity + 0.001f;
                var ax = math.cross(new float3(0, 1, 0), az);
                var ay = math.cross(az, ax);

                var freq = 8 + p.Random * 20;
                var flap = math.sin(freq * p.Time);

                ax = math.normalize(ax) * size;
                ay = math.normalize(ay) * size * flap;
                az = math.normalize(az) * size;

                var pos = Positions[index].Value;
                var face = Triangles[index];

                var va1 = pos + face.Vertex1;
                var va2 = pos + face.Vertex2;
                var va3 = pos + face.Vertex3;

                var vb1 = pos + az * 0.2f;
                var vb2 = pos - az * 0.2f;
                var vb3 = pos - ax + ay + az;
                var vb4 = pos - ax + ay - az;
                var vb5 = vb3 + ax * 2;
                var vb6 = vb4 + ax * 2;

                var p_t = math.saturate(p.Time);
                var v1 = math.lerp(va1, vb1, p_t);
                var v2 = math.lerp(va2, vb2, p_t);
                var v3 = math.lerp(va3, vb3, p_t);
                var v4 = math.lerp(va3, vb4, p_t);
                var v5 = math.lerp(va3, vb5, p_t);
                var v6 = math.lerp(va3, vb6, p_t);

                AddTriangle(v1, v2, v5);
                AddTriangle(v5, v2, v6);
                AddTriangle(v3, v4, v1);
                AddTriangle(v1, v4, v2);
            }
        }

        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(
                typeof(Renderer), typeof(ButterflyParticle), // shared
                typeof(Particle), typeof(Position), typeof(Triangle)
            );
        }

        unsafe protected override JobHandle OnUpdate(JobHandle deps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                _group.SetFilter(renderer);

                var count = _group.CalculateLength();
                if (count == 0) continue;

                // Create a reconstruction job and add it to the job chain.
                var job = new ReconstructionJob() {
                    Particles = _group.GetComponentDataArray<Particle>(),
                    Positions = _group.GetComponentDataArray<Position>(),
                    Triangles = _group.GetComponentDataArray<Triangle>(),
                    Vertices = UnsafeUtility.AddressOf(ref renderer.Vertices[0]),
                    Normals = UnsafeUtility.AddressOf(ref renderer.Normals[0]),
                    Counter = renderer.ConcurrentCounter
                };
                deps = job.Schedule(count, 8, deps);
            }

            _renderers.Clear();

            return deps;
        }
    }
}
