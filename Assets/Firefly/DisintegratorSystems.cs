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
            public float Time;
            public float DeltaTime;

            public void Execute(
                [ReadOnly] ref Disintegrator disintegrator,
                ref Position position
            )
            {
                var np = position.Value * 6;

                float3 grad1, grad2;
                noise.snoise(np, out grad1);
                noise.snoise(np + 100, out grad2);

                var acc = math.cross(grad1, grad2) * 0.02f;

                var dt = DeltaTime * math.saturate(Time - 2 + position.Value.y * 2);

                position.Value += disintegrator.Velocity * dt;
                disintegrator.Life += dt;
                disintegrator.Velocity += acc * dt;
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

    [UpdateAfter(typeof(DisintegratorAnimationSystem))]
    class DisintegratorReconstructionSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct ReconstructionJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Disintegrator> Disintegrators;
            [ReadOnly] public ComponentDataArray<Position> Positions;
            [ReadOnly] public ComponentDataArray<Facet> Facets;

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
                var t = Disintegrators[index].Life;

                var vz = math.normalize(Disintegrators[index].Velocity + 0.001f);
                var vx = math.normalize(math.cross(new float3(0, 1, 0), vz));
                var vy = math.cross(vz, vx);

                var f = Facets[index];

                var freq = 8 + Random.Value01((uint)index) * 20;
                vx *= 0.01f;
                vy *= 0.01f * math.sin(freq * t);
                vz *= 0.01f;

                var v1 = p;
                var v2 = p - vx - vz + vy;
                var v3 = p - vx + vz + vy;
                var v4 = p + vx + vz + vy;
                var v5 = p + vx - vz + vy;

                var tf = math.saturate(t);
                v1 = math.lerp(p + f.Vertex1, v1, tf);
                v2 = math.lerp(p + f.Vertex2, v2, tf);
                v3 = math.lerp(p + f.Vertex3, v3, tf);
                v4 = math.lerp(p + f.Vertex2, v4, tf);
                v5 = math.lerp(p + f.Vertex3, v5, tf);

                AddTriangle(v1, v2, v3);
                AddTriangle(v1, v3, v2);
                AddTriangle(v1, v4, v5);
                AddTriangle(v1, v5, v4);
            }
        }

        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(
                typeof(Disintegrator), typeof(Position), typeof(Facet), typeof(Renderer)
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
                    Disintegrators = _group.GetComponentDataArray<Disintegrator>(),
                    Positions = _group.GetComponentDataArray<Position>(),
                    Facets = _group.GetComponentDataArray<Facet>(),
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
