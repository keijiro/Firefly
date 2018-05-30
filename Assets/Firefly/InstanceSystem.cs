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
    class InstanceSystem : ComponentSystem
    {
        #region ComponentSystem implementation

        // Used to enumerate instance components
        List<Instance> _instanceDatas = new List<Instance>();
        ComponentGroup _instanceGroup;

        // Entity archetype used for instantiation
        EntityArchetype _archetype;

        // Allocation tracking
        List<Renderer> _toBeDisposed = new List<Renderer>();

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
                    var instanceEntity = instanceEntities[i];
                    var rendererSettings = EntityManager.
                        GetSharedComponentData<RenderSettings>(instanceEntity);

                    // Loop 3: Iterate over the vertices in the template mesh.
                    CreateEntitiesOverMesh(
                        transforms[i], rendererSettings, vertices, indices
                    );

                    // Remove the instance component from the entity.
                    EntityManager.RemoveComponent(instanceEntity, typeof(Instance));
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

        #region Jobified initializer

        // We use parallel-for jobs to calculate the initial data for the
        // components in the instanced entities. The primary motivation of this
        // is to optimize the vector math operations with Burst -- We don't
        // expect that parallelism gives a big performance boost.

        [ComputeJobOptimization]
        unsafe struct InitDataJob : IJobParallelFor
        {
            [ReadOnly, NativeDisableUnsafePtrRestriction] public void* Vertices;
            [ReadOnly, NativeDisableUnsafePtrRestriction] public void* Indices;
            [ReadOnly] public float4x4 Transform;

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
                    Random = Random.Value01((uint)i)
                };
            }
        }

        unsafe void CreateEntitiesOverMesh(
            UnityEngine.Transform transform,
            RenderSettings renderSettings,
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
                Triangles = new NativeArray<Triangle>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Positions = new NativeArray<Position>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                Particles = new NativeArray<Particle>(entityCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
            };
            var jobHandle = job.Schedule(entityCount, 32);

            // Create a renderer for this group.
            var renderer = new Renderer {
                Settings = renderSettings,
                WorkMesh = new UnityEngine.Mesh(),
                Vertices = new UnityEngine.Vector3 [Renderer.MaxVertices],
                Normals = new UnityEngine.Vector3 [Renderer.MaxVertices],
                Counter = new NativeCounter(Allocator.Persistent)
            };

            // We want this renderer object disposed at the end of world.
            _toBeDisposed.Add(renderer);

            // Create the default entity.
            var defaultEntity = EntityManager.CreateEntity(_archetype);
            EntityManager.SetSharedComponentData(defaultEntity, renderer);

            // Create an array of clones as putting a clone on each triangle.
            var entities = new NativeArray<Entity>(
                entityCount, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );
            EntityManager.Instantiate(defaultEntity, entities);

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
            EntityManager.DestroyEntity(defaultEntity);
            entities.Dispose();
            job.Triangles.Dispose();
            job.Positions.Dispose();
            job.Particles.Dispose();
        }

        #endregion
    }

    // Preview in edit mode
    [UnityEngine.ExecuteInEditMode]
    class InstancePreviewSystem : ComponentSystem
    {
        struct Group
        {
            [ReadOnly] public SharedComponentDataArray<Instance> Instances;
            [ReadOnly] public SharedComponentDataArray<RenderSettings> RenderSettings;
            [ReadOnly] public ComponentArray<UnityEngine.Transform> Transforms;
            public int Length;
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
