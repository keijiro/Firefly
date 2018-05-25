using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

public class FlyAnimationSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    unsafe struct ConstructionJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Fly> Flies;
        [ReadOnly] public ComponentDataArray<Facet> Facets;
        [ReadOnly] public ComponentDataArray<Position> Positions;
        [ReadOnly] public float Time;

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
            var n = MakeNormal(f.Vertex1, f.Vertex2, f.Vertex3);

            var offs = new float3(0, Time, 0);
            var d = noise.snoise(p * 8 + offs);
            d = math.pow(math.abs(d), 5);

            var v1 = p + f.Vertex1;
            var v2 = p + f.Vertex2;
            var v3 = p + f.Vertex3;
            var v4 = p + n * d;

            AddTriangle(v1, v2, v4);
            AddTriangle(v2, v3, v4);
            AddTriangle(v3, v1, v4);
        }
    }

    List<FlyRenderer> _renderers = new List<FlyRenderer>();
    ComponentGroup _flyGroup;

    protected override void OnCreateManager(int capacity)
    {
        _flyGroup = GetComponentGroup(
            typeof(Fly), typeof(Facet), typeof(Position), typeof(FlyRenderer)
        );
    }

    unsafe protected override JobHandle OnUpdate(JobHandle deps)
    {
        EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

        for (var i = 0; i < _renderers.Count; i++)
        {
            var renderer = _renderers[i];
            if (renderer.MeshInstance == null) continue;

            renderer.Counter.Count = 0;

            _flyGroup.SetFilter(renderer);

            var job = new ConstructionJob() {
                Flies = _flyGroup.GetComponentDataArray<Fly>(),
                Facets = _flyGroup.GetComponentDataArray<Facet>(),
                Positions = _flyGroup.GetComponentDataArray<Position>(),
                Time = UnityEngine.Time.time,
                Vertices = renderer.Vertices,
                Normals = renderer.Normals,
                Counter = renderer.Counter
            };

            deps = job.Schedule(_flyGroup.CalculateLength(), 8, deps);
        }

        _renderers.Clear();

        return deps;
    }
}
