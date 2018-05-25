using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    class DisintegratorAnimationSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct AnimationJob : IJobProcessComponentData<Disintegrator, Position>
        {
            public float dt;

            public void Execute(
                [ReadOnly] ref Disintegrator disintegrator,
                ref Position position
            )
            {
                float3 np = position.Value * 2;

                float3 grad1, grad2;
                noise.snoise(np, out grad1);
                noise.snoise(np + 100, out grad2);

                float3 acc = math.cross(grad1, grad2) * 0.02f;

                position.Value += disintegrator.Velocity * dt;
                disintegrator.Life += dt;
                disintegrator.Velocity += acc * dt;
            }
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
           var job = new AnimationJob() { dt = UnityEngine.Time.deltaTime };
           return job.Schedule(this, 32, deps);
        }
    }

    [UpdateAfter(typeof(DisintegratorAnimationSystem))]
    class DisintegratorReconstructionSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct ReconstructionJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Facet> Facets;
            [ReadOnly] public ComponentDataArray<Position> Positions;

            [NativeDisableParallelForRestriction] public NativeArray<float3> Vertices;
            [NativeDisableParallelForRestriction] public NativeArray<float3> Normals;

            public NativeCounter.Concurrent Counter;

            float3 MakeNormal(float3 a, float3 b, float3 c)
            {
                return math.normalize(math.cross(b - a, c - a));
            }

            void AddTriangle(float3 v1, float3 v2, float3 v3)
            {
                var n = MakeNormal(v1, v2, v3);
                var vi = Counter.Increment() * 3;

                Vertices[vi + 0] = v1;
                Vertices[vi + 1] = v2;
                Vertices[vi + 2] = v3;

                Normals[vi + 0] = n;
                Normals[vi + 1] = n;
                Normals[vi + 2] = n;
            }

            public void Execute(int index)
            {
                var p = Positions[index].Value;
                var f = Facets[index];

                var v1 = p + f.Vertex1;
                var v2 = p + f.Vertex2;
                var v3 = p + f.Vertex3;
                var v4 = p - (f.Vertex2 - f.Vertex1);
                var v5 = p - (f.Vertex3 - f.Vertex1);

                AddTriangle(v1, v2, v3);
                AddTriangle(v1, v4, v5);
            }
        }

        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(
                typeof(Disintegrator), typeof(Facet), typeof(Position), typeof(Renderer)
            );
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer.WorkMesh == null) continue;

                renderer.Counter.Count = 0;
                _group.SetFilter(renderer);

                var job = new ReconstructionJob() {
                    Facets = _group.GetComponentDataArray<Facet>(),
                    Positions = _group.GetComponentDataArray<Position>(),
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
