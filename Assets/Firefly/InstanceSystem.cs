using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

namespace Firefly
{
    class InstanceSystem : ComponentSystem
    {
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
                typeof(Disintegrator), typeof(Facet), typeof(Position), typeof(Renderer)
            );
        }

        protected override void OnDestroyManager()
        {
            foreach (var renderer in _toBeDisposed)
            {
                renderer.Vertices.Dispose();
                renderer.Normals.Dispose();
                renderer.Counter.Dispose();
            }
        }

        void Instantiate(
            UnityEngine.Transform transform,
            RenderSettings renderSettings,
            UnityEngine.Vector3 [] vertices, int [] indices
        )
        {
            // Calculate the transform matrix.
            var matrix = (float4x4)UnityEngine.Matrix4x4.TRS(
                transform.position, transform.rotation, transform.localScale
            );

            // Create a renderer for this group.
            var renderer = new Renderer {
                Settings = renderSettings,
                WorkMesh = new UnityEngine.Mesh(),
                Vertices = new NativeArray<float3>(Renderer.kMaxVertices, Allocator.Persistent),
                Normals = new NativeArray<float3>(Renderer.kMaxVertices, Allocator.Persistent),
                Counter = new NativeCounter(Allocator.Persistent)
            };

            _toBeDisposed.Add(renderer);

            // Create the template entity.
            var template = EntityManager.CreateEntity(_archetype);
            EntityManager.SetSharedComponentData(template, renderer);

            // Clone the template entity.
            var clones = new NativeArray<Entity>(indices.Length / 3, Allocator.Temp);
            EntityManager.Instantiate(template, clones);

            // Set the initial data.
            for (var i = 0; i < clones.Length; i++)
            {
                var v1 = math.mul(matrix, new float4(vertices[indices[i * 3 + 0]], 1)).xyz;
                var v2 = math.mul(matrix, new float4(vertices[indices[i * 3 + 1]], 1)).xyz;
                var v3 = math.mul(matrix, new float4(vertices[indices[i * 3 + 2]], 1)).xyz;
                var vc = (v1 + v2 + v3) / 3;

                var entity = clones[i];

                EntityManager.SetComponentData(entity, new Facet {
                    Vertex1 = v1 - vc, Vertex2 = v2 - vc, Vertex3 = v3 - vc
                });

                EntityManager.SetComponentData(entity, new Position { Value = vc });
            }

            // Destroy the temporary objects.
            EntityManager.DestroyEntity(template);
            clones.Dispose();
        }

        protected override void OnUpdate()
        {
            // Enumerate all the instance data entries.
            EntityManager.GetAllUniqueSharedComponentDatas(_instanceDatas);
            foreach (var instanceData in _instanceDatas)
            {
                // Skip if it has no data.
                if (instanceData.templateMesh == null) continue;

                // Get a copy of the instance entity array.
                // Don't directly use the iterator -- we're going to remove
                // the instance components, and it will invalidate the iterator.
                _instanceGroup.SetFilter(instanceData);
                var iterator = _instanceGroup.GetEntityArray();
                if (iterator.Length == 0) continue;
                var instanceEntities = new NativeArray<Entity>(iterator.Length, Allocator.Temp);
                iterator.CopyTo(instanceEntities);

                // Accessor to the scene transform
                var transforms = _instanceGroup.GetTransformAccessArray();

                // Retrieve the mesh data.
                var vertices = instanceData.templateMesh.vertices;
                var indices = instanceData.templateMesh.triangles;

                // Instantiate flies along with the instance entities.
                for (var instanceIndex = 0; instanceIndex < instanceEntities.Length; instanceIndex++)
                {
                    var instanceEntity = instanceEntities[instanceIndex];

                    Instantiate(
                        transforms[instanceIndex],
                        EntityManager.GetSharedComponentData<RenderSettings>(instanceEntity),
                        vertices, indices
                    );

                    // Remove the instance component from the entity.
                    EntityManager.RemoveComponent(instanceEntity, typeof(Instance));
                }

                instanceEntities.Dispose();
            }

            _instanceDatas.Clear();
        }
    }
}
