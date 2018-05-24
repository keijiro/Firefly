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
                Vertices = renderer.Vertices,
                Normals = renderer.Normals
            };

            deps = job.Schedule(_flyGroup.CalculateLength(), 64, deps);
        }

        _renderers.Clear();

        return deps;
    }
}
