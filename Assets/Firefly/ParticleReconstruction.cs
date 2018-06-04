using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using System.Collections.Generic;

namespace Firefly
{
    interface IParticleReconstructionJob<TVariant>
    {
        void Initialize(
            TVariant variant,
            ComponentGroup group,
            UnityEngine.Vector3 [] vertices,
            UnityEngine.Vector3 [] normals,
            NativeCounter.Concurrent counter
        );
    }

    class ParticleReconstructionSystemBase<TVariant, TJob> : JobComponentSystem
        where TVariant : struct, ISharedComponentData, IParticleVariant
        where TJob : struct, IJobParallelFor, IParticleReconstructionJob<TVariant>
    {
        List<Renderer> _renderers = new List<Renderer>();
        List<TVariant> _variants = new List<TVariant>();

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
            EntityManager.GetAllUniqueSharedComponentDatas(_variants);

            var job = new TJob();

            for (var i1 = 0; i1 < _renderers.Count; i1++)
            {
                var renderer = _renderers[i1];

                for (var i2 = 0; i2 < _variants.Count; i2++)
                {
                    var variant = _variants[i2];

                    _group.SetFilter(renderer, variant);

                    var groupCount = _group.CalculateLength();
                    if (groupCount == 0) continue;

                    job.Initialize(
                        variant, _group,
                        renderer.Vertices, renderer.Normals,
                        renderer.ConcurrentCounter
                    );

                    deps = job.Schedule(groupCount, 8, deps);
                }
            }

            _renderers.Clear();
            _variants.Clear();

            return deps;
        }
    }
}
