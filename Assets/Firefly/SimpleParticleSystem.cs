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
                var pos = Positions[index].Value;
                var face = Triangles[index];

                var v1 = pos + face.Vertex1;
                var v2 = pos + face.Vertex2;
                var v3 = pos + face.Vertex3;

                AddTriangle(v1, v2, v3);
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

                deps = job.Schedule(_group.CalculateLength(), 16, deps);
            }

            _renderers.Clear();

            return deps;
        }
    }
}
