using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

class FlySpawnerSystem : ComponentSystem
{
    // Used to enumerate spawner components
    List<FlySpawner> _spawners = new List<FlySpawner>();
    ComponentGroup _group;

    // Fly entity archetype used for instantiation
    EntityArchetype _flyArchetype;

    protected override void OnCreateManager(int capacity)
    {
        _group = GetComponentGroup(
            typeof(FlySpawner), typeof(Position)
        );

        _flyArchetype = EntityManager.CreateArchetype(
            typeof(Fly), typeof(Facet), typeof(Position)
        );
    }

    protected override void OnUpdate()
    {
        // Enumerate all the spawners.
        EntityManager.GetAllUniqueSharedComponentDatas(_spawners);
        foreach (var spawner in _spawners)
        {
            // Skip if it has no data.
            if (spawner.templateMesh == null) continue;

            // Retrieve the mesh data.
            var vertices = spawner.templateMesh.vertices;
            var indices = spawner.templateMesh.triangles;

            // Get a copy of the entity array.
            // Don't directly use the iterator -- we're going to remove
            // the spawner components, and it will invalidate the iterator.
            _group.SetFilter(spawner);
            var iterator = _group.GetEntityArray();
            var entities = new NativeArray<Entity>(iterator.Length, Allocator.Temp);
            iterator.CopyTo(entities);

            // Instantiate flies along with the spawner entities.
            for (var j = 0; j < entities.Length; j++)
            {
                // Retrieve the position data.
                var position = EntityManager.GetComponentData<Position>(entities[j]).Value;

                for (var vi = 0; vi < indices.Length; vi += 3)
                {
                    var v1 = (float3)vertices[indices[vi + 0]];
                    var v2 = (float3)vertices[indices[vi + 1]];
                    var v3 = (float3)vertices[indices[vi + 2]];
                    var vc = (v1 + v2 + v3) / 3;

                    var fly = EntityManager.CreateEntity(_flyArchetype);

                    EntityManager.SetComponentData(fly, new Facet {
                        Vertex1 = v1 - vc,
                        Vertex2 = v2 - vc,
                        Vertex3 = v3 - vc
                    });

                    EntityManager.SetComponentData(fly, new Position {
                        Value = position + vc
                    });
                }

                // Remove the spawner component from the entity.
                EntityManager.RemoveComponent(entities[j], typeof(FlySpawner));
            }

            entities.Dispose();
        }

        _spawners.Clear();
    }
}
