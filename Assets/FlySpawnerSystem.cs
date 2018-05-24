using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

class FlySpawnerSystem : ComponentSystem
{
    // Used to enumerate spawner components
    List<FlySpawner> _spawnerDatas = new List<FlySpawner>();
    ComponentGroup _spawnerGroup;

    // Fly entity archetype used for instantiation
    EntityArchetype _flyArchetype;

    protected override void OnCreateManager(int capacity)
    {
        _spawnerGroup = GetComponentGroup(
            typeof(Position), typeof(FlySpawner), typeof(FlyRenderer)
        );

        _flyArchetype = EntityManager.CreateArchetype(
            typeof(Fly), typeof(Facet), typeof(Position), typeof(FlyRenderer)
        );
    }

    protected override void OnUpdate()
    {
        // Enumerate all the spawner data entries.
        EntityManager.GetAllUniqueSharedComponentDatas(_spawnerDatas);
        foreach (var spawnerData in _spawnerDatas)
        {
            // Skip if it has no data.
            if (spawnerData.templateMesh == null) continue;

            // Get a copy of the spawner entity array.
            // Don't directly use the iterator -- we're going to remove
            // the spawner components, and it will invalidate the iterator.
            _spawnerGroup.SetFilter(spawnerData);
            var iterator = _spawnerGroup.GetEntityArray();
            if (iterator.Length == 0) continue;
            var spawnerEntities = new NativeArray<Entity>(iterator.Length, Allocator.Temp);
            iterator.CopyTo(spawnerEntities);

            // Retrieve the mesh data.
            var vertices = spawnerData.templateMesh.vertices;
            var indices = spawnerData.templateMesh.triangles;

            // Instantiate flies along with the spawner entities.
            for (var j = 0; j < spawnerEntities.Length; j++)
            {
                // Retrieve the source data.
                var spawnerEntity = spawnerEntities[j];
                var position = EntityManager.GetComponentData<Position>(spawnerEntity).Value;
                var renderer = EntityManager.GetSharedComponentData<FlyRenderer>(spawnerEntity);

                // Populate fly entities.
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

                    EntityManager.SetSharedComponentData(fly, renderer);
                }

                // Remove the spawner component from the entity.
                EntityManager.RemoveComponent(spawnerEntity, typeof(FlySpawner));
            }

            spawnerEntities.Dispose();
        }

        _spawnerDatas.Clear();
    }
}
