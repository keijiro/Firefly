using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

public class FlyAnimationSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct ConstructionJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Fly> Flies;
        [ReadOnly] public ComponentDataArray<Facet> Facets;
        [ReadOnly] public ComponentDataArray<Position> Positions;
        [ReadOnly] public float Time;

        [NativeDisableParallelForRestriction] public NativeArray<float3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Normals;

        public void Execute(int index)
        {
            var p = Positions[index].Value;
            var f = Facets[index];
            var vi = index * 3;

            var v1 = p + f.Vertex1;
            var v2 = p + f.Vertex2;
            var v3 = p + f.Vertex3;

            var offs = new float3(0, 0, Time * 0.6f);
            float3 d1, d2, d3;

            noise.snoise(v1 + offs, out d1);
            noise.snoise(v2 + offs, out d2);
            noise.snoise(v3 + offs, out d3);

            v1 += d1 * 0.05f;
            v2 += d2 * 0.05f;
            v3 += d3 * 0.05f;

            var n = math.normalize(math.cross(v2 - v1, v3 - v1));

            Vertices[vi + 0] = v1;
            Vertices[vi + 1] = v2;
            Vertices[vi + 2] = v3;

            Normals[vi + 0] = n;
            Normals[vi + 1] = n;
            Normals[vi + 2] = n;
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

    protected override JobHandle OnUpdate(JobHandle deps)
    {
        EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

        foreach (var renderer in _renderers)
        {
            if (renderer.MeshInstance == null) continue;

            _flyGroup.SetFilter(renderer);

            var job = new ConstructionJob() {
                Flies = _flyGroup.GetComponentDataArray<Fly>(),
                Facets = _flyGroup.GetComponentDataArray<Facet>(),
                Positions = _flyGroup.GetComponentDataArray<Position>(),
                Time = UnityEngine.Time.time,
                Vertices = renderer.Vertices,
                Normals = renderer.Normals
            };

            deps = job.Schedule(_flyGroup.CalculateLength(), 16, deps);
        }

        _renderers.Clear();

        return deps;
    }
}
