#define DEBUG_DIAGNOSTICS

using Unity.Collections;
using Unity.Entities;
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
        #if DEBUG_DIAGNOSTICS
            var stopwatch = new Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();
        #endif

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
             UnityEngine.Debug.Log("Instantiation: " + time + " ms");
        #endif
        }

        #endregion

        #region Internal methods

        void CreateEntitiesOverMesh(
            UnityEngine.Transform transform,
            RenderSettings renderSettings,
            UnityEngine.Vector3 [] vertices,
            int [] indices
        )
        {
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
                indices.Length / 3, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory
            );
            EntityManager.Instantiate(defaultEntity, entities);

            // Calculate the transform matrix.
            var matrix = transform.localToWorldMatrix;

            // Set the initial data.
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                var v1 = math.mul(matrix, new float4(vertices[indices[i * 3 + 0]], 1)).xyz;
                var v2 = math.mul(matrix, new float4(vertices[indices[i * 3 + 1]], 1)).xyz;
                var v3 = math.mul(matrix, new float4(vertices[indices[i * 3 + 2]], 1)).xyz;
                var vc = (v1 + v2 + v3) / 3;

                EntityManager.SetComponentData(entity, new Triangle {
                    Vertex1 = v1 - vc, Vertex2 = v2 - vc, Vertex3 = v3 - vc
                });

                EntityManager.SetComponentData(entity, new Position {
                    Value = vc
                });

                EntityManager.SetComponentData(entity, new Particle {
                    Random = Random.Value01((uint)i)
                });
            }

            // Destroy the temporary objects.
            EntityManager.DestroyEntity(defaultEntity);
            entities.Dispose();
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
