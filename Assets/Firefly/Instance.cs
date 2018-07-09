#define DEBUG_DIAGNOSTICS

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;
using System.Diagnostics;

namespace Firefly
{
    sealed class InstanceSystem : ComponentSystem
    {
        #region ComponentSystem implementation

        // Used to enumerate instance components
        List<Instance> _instanceDatas = new List<Instance>();
        ComponentGroup _instanceGroup;

        // Entity archetype used for instantiation
        EntityArchetype _archetype;

        // Allocation tracking
        List<Renderer> _toBeDisposed = new List<Renderer>();

        // Used to give IDs to particles.
        uint _indexCounter;

        protected override void OnCreateManager(int capacity)
        {
            _instanceGroup = GetComponentGroup(
                typeof(Instance), typeof(RenderSettings), typeof(UnityEngine.Transform)
            );

            _archetype = EntityManager.CreateArchetype(
                typeof(Particle), typeof(Triangle), typeof(Position), typeof(Renderer)
            );
        }

        protected override void OnDestroyManager()
        {
            foreach (var renderer in _toBeDisposed)
            {
                UnityEngine.Object.Destroy(renderer.WorkMesh);
                renderer.Counter.Dispose();
            }
            _toBeDisposed.Clear();
        }

        protected override void OnUpdate()
        {
            //
            // There are three levels of loops in this system:
            //
            // Loop 1: Through the array of unique instance settings. We'll get
            // an array of entities that share the same instance setting.
            //
            // Loop 2: Through the array of entities got in Loop 1.
            //
            // Loop 3: Through the array of vertices in the template mesh given
            // via the instance setting.
            //

        #if DEBUG_DIAGNOSTICS
            var stopwatch = new Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();
        #endif

            // Loop 1: Iterate over the unique instance data entries.
            EntityManager.GetAllUniqueSharedComponentDatas(_instanceDatas);
            foreach (var instanceData in _instanceDatas)
            {
                // Skip if it doesn't have any data.
                if (instanceData.TemplateMesh == null) continue;

                // Get a copy of the entity array. We shouldn't directly use
                // the iterator because we're going to remove the instance
                // components that invalidates the iterator.
                _instanceGroup.SetFilter(instanceData);

                var iterator = _instanceGroup.GetEntityArray();
                if (iterator.Length == 0) continue;

                var instanceEntities = new NativeArray<Entity>(
                    iterator.Length, Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory
                );
                iterator.CopyTo(instanceEntities);

                // Accessor to the scene transforms
                var transforms = _instanceGroup.GetTransformAccessArray();

                // Retrieve the template mesh data.
                var vertices = instanceData.TemplateMesh.vertices;
                var indices = instanceData.TemplateMesh.triangles;

                // Loop 2: Iterate over the instance entities.
                for (var i = 0; i < instanceEntities.Length; i++)
                {
                    // Loop 3: Iterate over the vertices in the template mesh.
                    CreateEntitiesOverMesh(instanceEntities[i], transforms[i], vertices, indices); 

                    // Remove the instance component from the entity.
                    EntityManager.RemoveComponent(instanceEntities[i], typeof(Instance));
                }

                instanceEntities.Dispose();
            }

            _instanceDatas.Clear();

        #if DEBUG_DIAGNOSTICS
             stopwatch.Stop();
             var time = 1000.0 * stopwatch.ElapsedTicks / Stopwatch.Frequency;
             UnityEngine.Debug.Log("Instantiation time: " + time + " ms");
        #endif
        }

        #endregion

        #region Default entity table

        // This table is used to create particle entities with a weighted
        // random distribution of particle types. It stores selection weights
        // and default entities that allows creating entities with instancing.

        struct DefaultEntityEntry
        {
            public float Weight;
            public Entity Entity;
        }

        DefaultEntityEntry[] _defaultEntities = new DefaultEntityEntry[16];

        DefaultEntityEntry CreateDefaultEntity<T>(Entity sourceEntity, ref Renderer renderer)
            where T : struct, ISharedComponentData, IParticleVariant
        {
            var variant = EntityManager.GetSharedComponentData<T>(sourceEntity);
            var entity = EntityManager.CreateEntity(_archetype);
            EntityManager.SetSharedComponentData(entity, renderer);
            EntityManager.AddSharedComponentData(entity, variant);
            return new DefaultEntityEntry { Weight = variant.GetWeight(), Entity = entity };
        }

        void NormalizeDefaultEntityWeights()
        {
            var total = 0.0f;
            for (var i = 0; i < _defaultEntities.Length; i++)
                total += _defaultEntities[i].Weight;

            var subtotal = 0.0f;
            for (var i = 0; i < _defaultEntities.Length; i++)
            {
                subtotal += _defaultEntities[i].Weight / total;
                _defaultEntities[i].Weight = subtotal;
            }
        }

        Entity SelectRandomDefaultEntity(uint seed)
        {
            var rand = Random.Value01(seed);
            for (var i = 0; i < _defaultEntities.Length; i++)
                if (rand < _defaultEntities[i].Weight)
                    return _defaultEntities[i].Entity;
            return Entity.Null;
        }

        void CleanupDefaultEntityTable()
        {
            for (var i = 0; i < _defaultEntities.Length; i++)
            {
                var entity = _defaultEntities[i].Entity;
                if (EntityManager.Exists(entity))
                    EntityManager.DestroyEntity(entity);
                _defaultEntities[i] = default(DefaultEntityEntry);
            }
        }

        #endregion

        #region Jobified initializer

        // We use parallel-for jobs to calculate the initial data for the
        // components in the instanced entities. The primary motivation of this
        // is to optimize the vector math operations with Burst -- We don't
        // expect that parallelism gives a big performance boost.

        [Unity.Burst.BurstCompile]
        unsafe struct InitDataJob : IJobParallelFor
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction] public void* Vertices;
            [ReadOnly, NativeDisableUnsafePtrRestriction] public void* Indices;

            public float4x4 Transform;
            public uint IndexOffset;

            public NativeArray<Triangle> Triangles;
            public NativeArray<Position> Positions;
            public NativeArray<Particle> Particles;

            public void Execute(int i)
            {
                var i1 = UnsafeUtility.ReadArrayElement<int>(Indices, i * 3);
                var i2 = UnsafeUtility.ReadArrayElement<int>(Indices, i * 3 + 1);
                var i3 = UnsafeUtility.ReadArrayElement<int>(Indices, i * 3 + 2);

                var v1 = UnsafeUtility.ReadArrayElement<float3>(Vertices, i1);
                var v2 = UnsafeUtility.ReadArrayElement<float3>(Vertices, i2);
                var v3 = UnsafeUtility.ReadArrayElement<float3>(Vertices, i3);

                v1 = math.mul(Transform, new float4(v1, 1)).xyz;
                v2 = math.mul(Transform, new float4(v2, 1)).xyz;
                v3 = math.mul(Transform, new float4(v3, 1)).xyz;

                var vc = (v1 + v2 + v3) / 3;

                Triangles[i] = new Triangle {
                    Vertex1 = v1 - vc,
                    Vertex2 = v2 - vc,
                    Vertex3 = v3 - vc
                };

                Positions[i] = new Position {
                    Value = vc
                };

                Particles[i] = new Particle {
                    ID = (uint)i + IndexOffset,
                    LifeRandom = Random.Value01((uint)i) * 0.8f + 0.2f
                };
            }
        }

        unsafe void CreateEntitiesOverMesh(
            Entity sourceEntity,
            UnityEngine.Transform transform,
            UnityEngine.Vector3 [] vertices,
            int [] indices
        )
        {
            var entityCount = indices.Length / 3;

            // Calculate the initial data with parallel-for jobs.
            var job = new InitDataJob {
                Vertices = UnsafeUtility.AddressOf(ref vertices[0]),
                Indices = UnsafeUtility.AddressOf(ref indices[0]),
                Transform = transform.localToWorldMatrix,
                IndexOffset = _indexCounter,
                Triangles = new NativeArray<Triangle>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Positions = new NativeArray<Position>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Particles = new NativeArray<Particle>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
            };
            var jobHandle = job.Schedule(entityCount, 32);
            _indexCounter += (uint)entityCount;

            // We want to do entity instantiation in parallel with the jobs,
            // so let the jobs kick in immediately.
            JobHandle.ScheduleBatchedJobs();

            // Create a renderer for this group.
            var counter = new NativeCounter(Allocator.Persistent);
            var renderer = new Renderer {
                Settings = EntityManager.GetSharedComponentData<RenderSettings>(sourceEntity),
                WorkMesh = new UnityEngine.Mesh(),
                Vertices = new UnityEngine.Vector3 [Renderer.MaxVertices],
                Normals = new UnityEngine.Vector3 [Renderer.MaxVertices],
                Counter = counter, ConcurrentCounter = counter
            };

            // We want this renderer object disposed at the end of world.
            _toBeDisposed.Add(renderer);

            // Initialize the default entity table.
            _defaultEntities[0] = CreateDefaultEntity<SimpleParticle>(sourceEntity, ref renderer);
            _defaultEntities[1] = CreateDefaultEntity<ButterflyParticle>(sourceEntity, ref renderer);
            NormalizeDefaultEntityWeights();

            // Create an array of clones as putting a clone on each triangle.
            var entities = new NativeArray<Entity>(
                entityCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );

            for (var i = 0; i < entityCount; i++)
            {
                entities[i] = EntityManager.Instantiate(
                    SelectRandomDefaultEntity((uint)i)
                );
            }

            // Set the initial data.
            jobHandle.Complete();

            for (var i = 0; i < entityCount; i++)
            {
                var entity = entities[i];
                EntityManager.SetComponentData(entity, job.Triangles[i]);
                EntityManager.SetComponentData(entity, job.Positions[i]);
                EntityManager.SetComponentData(entity, job.Particles[i]);
            }

            // Destroy the temporary objects.
            entities.Dispose();

            CleanupDefaultEntityTable();

            job.Triangles.Dispose();
            job.Positions.Dispose();
            job.Particles.Dispose();
        }

        #endregion
    }

    // Preview in edit mode
    [UnityEngine.ExecuteInEditMode]
    sealed class InstancePreviewSystem : ComponentSystem
    {
        struct Group
        {
            [ReadOnly] public SharedComponentDataArray<Instance> Instances;
            [ReadOnly] public SharedComponentDataArray<RenderSettings> RenderSettings;
            [ReadOnly] public ComponentArray<UnityEngine.Transform> Transforms;
            public readonly int Length;
        }

        [Inject] Group _group;

        protected override void OnUpdate()
        {
            for (var i = 0; i < _group.Length; i++)
            {
                UnityEngine.Graphics.DrawMesh(
                    _group.Instances[i].TemplateMesh,
                    _group.Transforms[i].localToWorldMatrix,
                    _group.RenderSettings[i].Material, 0
                );
            }
        }
    }
}
