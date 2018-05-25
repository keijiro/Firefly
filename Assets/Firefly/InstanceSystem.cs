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
                typeof(Position), typeof(Instance), typeof(RenderSettings)
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

                // Retrieve the mesh data.
                var vertices = instanceData.templateMesh.vertices;
                var indices = instanceData.templateMesh.triangles;

                // Instantiate flies along with the instance entities.
                for (var j = 0; j < instanceEntities.Length; j++)
                {
                    // Retrieve the source data.
                    var instanceEntity = instanceEntities[j];
                    var position = EntityManager.GetComponentData<Position>(instanceEntity).Value;

                    // Create a renderer for this group.
                    var renderer = new Renderer();
                    renderer.Settings = EntityManager.GetSharedComponentData<RenderSettings>(instanceEntity);
                    renderer.WorkMesh = new UnityEngine.Mesh();
                    renderer.Vertices = new NativeArray<float3>(Renderer.kMaxVertices, Allocator.Persistent);
                    renderer.Normals = new NativeArray<float3>(Renderer.kMaxVertices, Allocator.Persistent);
                    renderer.Counter = new NativeCounter(Allocator.Persistent);

                    _toBeDisposed.Add(renderer);

                    // Populate entities.
                    for (var vi = 0; vi < indices.Length; vi += 3)
                    {
                        var v1 = (float3)vertices[indices[vi + 0]];
                        var v2 = (float3)vertices[indices[vi + 1]];
                        var v3 = (float3)vertices[indices[vi + 2]];
                        var vc = (v1 + v2 + v3) / 3;

                        var entity = EntityManager.CreateEntity(_archetype);

                        EntityManager.SetComponentData(entity, new Facet {
                            Vertex1 = v1 - vc,
                            Vertex2 = v2 - vc,
                            Vertex3 = v3 - vc
                        });

                        EntityManager.SetComponentData(entity, new Position {
                            Value = position + vc
                        });

                        EntityManager.SetSharedComponentData(entity, renderer);
                    }

                    // Remove the instance component from the entity.
                    EntityManager.RemoveComponent(instanceEntity, typeof(Instance));
                }

                instanceEntities.Dispose();
            }

            _instanceDatas.Clear();
        }
    }
}
