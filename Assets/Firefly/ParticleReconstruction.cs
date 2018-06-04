using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using System.Collections.Generic;

namespace Firefly
{
    interface IParticleReconstructionJob
    {
        void Initialize(
            ComponentGroup group,
            UnityEngine.Vector3 [] vertices,
            UnityEngine.Vector3 [] normals,
            NativeCounter.Concurrent counter
        );
    }

    class ParticleReconstructionSystemBase<TVariant, TJob> : JobComponentSystem
        where TVariant : struct, ISharedComponentData, IParticleVariant
        where TJob : struct, IJobParallelFor, IParticleReconstructionJob
    {
        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _group;

        protected override void OnCreateManager(int capacity)
        {
            _group = GetComponentGroup(
                typeof(Renderer), typeof(TVariant),
                typeof(Particle), typeof(Position), typeof(Triangle)
            );
        }

        protected override JobHandle OnUpdate(JobHandle deps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            TJob job = new TJob();

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                _group.SetFilter(renderer);

                var groupCount = _group.CalculateLength();
                if (groupCount == 0) continue;

                job.Initialize(
                    _group,
                    renderer.Vertices, renderer.Normals,
                    renderer.ConcurrentCounter
                );
                deps = job.Schedule(groupCount, 8, deps);
            }

            _renderers.Clear();

            return deps;
        }
    }
}
