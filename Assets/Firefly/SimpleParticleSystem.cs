using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    sealed class SimpleParticleSystem : JobComponentSystem
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

            public void Execute(int index)
            {
                var particle = Particles[index];
                var face = Triangles[index];

                var fwd = particle.Velocity + 1e-4f;
                var axis = math.normalize(math.cross(fwd, face.Vertex1));
                var rot = math.axisAngle(axis, particle.Life * 3);

                var pos = Positions[index].Value;
                var v1 = pos + math.mul(rot, face.Vertex1);
                var v2 = pos + math.mul(rot, face.Vertex2);
                var v3 = pos + math.mul(rot, face.Vertex3);

                var i = Counter.Increment() * 3;
                UnsafeUtility.WriteArrayElement(Vertices, i + 0, v1);
                UnsafeUtility.WriteArrayElement(Vertices, i + 1, v2);
                UnsafeUtility.WriteArrayElement(Vertices, i + 2, v3);

                var n = math.normalize(math.cross(v2 - v1, v3 - v1));
                UnsafeUtility.WriteArrayElement(Normals, i + 0, n);
                UnsafeUtility.WriteArrayElement(Normals, i + 1, n);
                UnsafeUtility.WriteArrayElement(Normals, i + 2, n);
            }
        }

        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(
                typeof(Renderer), typeof(SimpleParticle), // shared
                typeof(Particle), typeof(Position), typeof(Triangle)
            );
        }

        unsafe protected override JobHandle OnUpdate(JobHandle deps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer.WorkMesh == null) continue;

                _group.SetFilter(renderer);
                if (_group.CalculateLength() == 0) continue;

                // Create a reconstruction job and add it to the job chain.
                var job = new ReconstructionJob() {
                    Particles = _group.GetComponentDataArray<Particle>(),
                    Positions = _group.GetComponentDataArray<Position>(),
                    Triangles = _group.GetComponentDataArray<Triangle>(),
                    Vertices = UnsafeUtility.AddressOf(ref renderer.Vertices[0]),
                    Normals = UnsafeUtility.AddressOf(ref renderer.Normals[0]),
                    Counter = renderer.ConcurrentCounter
                };

                deps = job.Schedule(_group.CalculateLength(), 8, deps);
            }

            _renderers.Clear();

            return deps;
        }
    }
}
