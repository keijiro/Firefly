using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    class ParticleReconstructionSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct ReconstructionJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Particle> Particles;
            [ReadOnly] public ComponentDataArray<Position> Positions;
            [ReadOnly] public ComponentDataArray<Triangle> Triangles;

            [NativeDisableParallelForRestriction] public NativeArray<float3> Vertices;
            [NativeDisableParallelForRestriction] public NativeArray<float3> Normals;

            public NativeCounter.Concurrent Counter;

            void AddTriangle(float3 v1, float3 v2, float3 v3)
            {
                var i = Counter.Increment() * 3;

                Vertices[i + 0] = v1;
                Vertices[i + 1] = v2;
                Vertices[i + 2] = v3;

                Normals[i + 0] = Normals[i + 1] = Normals[i + 2] =
                    math.normalize(math.cross(v2 - v1, v3 - v1));
            }

            public void Execute(int index)
            {
                const float size = 0.005f;

                var p = Particles[index];

                var az = p.Velocity + 0.001f;
                var ax = math.cross(new float3(0, 1, 0), az);
                var ay = math.cross(az, ax);

                var freq = 8 + p.Random * 20;
                var flap = math.sin(freq * p.Life);

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

                var p_t = math.saturate(p.Life);
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
                typeof(Renderer), // shared
                typeof(Particle), typeof(Position), typeof(Triangle)
            );
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer.WorkMesh == null) continue;

                _group.SetFilter(renderer);

                // Reset the triangle counter.
                renderer.Counter.Count = 0;

                // Create a reconstruction job and add it to the job chain.
                var job = new ReconstructionJob() {
                    Particles = _group.GetComponentDataArray<Particle>(),
                    Positions = _group.GetComponentDataArray<Position>(),
                    Triangles = _group.GetComponentDataArray<Triangle>(),
                    Vertices = renderer.Vertices,
                    Normals = renderer.Normals,
                    Counter = renderer.Counter
                };

                deps = job.Schedule(_group.CalculateLength(), 16, deps);
            }

            _renderers.Clear();

            return deps;
        }
    }
}
