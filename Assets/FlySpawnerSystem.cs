using Unity.Collections;
using Unity.Entities;
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
        _group = GetComponentGroup(typeof(FlySpawner));
        _flyArchetype = EntityManager.CreateArchetype(
            typeof(Fly), typeof(Position)
        );
    }

    protected override void OnUpdate()
    {
        // Enumerate all the spawners.
        EntityManager.GetAllUniqueSharedComponentDatas(_spawners);
        for (var i = 0; i < _spawners.Count; i++)
        {
            _group.SetFilter(_spawners[i]);

            // Get a copy of the entity array.
            // Don't directly use the iterator -- we're going to remove
            // the buffer components, and it will invalidate the iterator.
            var iterator = _group.GetEntityArray();
            var entities = new NativeArray<Entity>(iterator.Length, Allocator.Temp);
            iterator.CopyTo(entities);

            // Instantiate flies along with the spawner entities.
            for (var j = 0; j < entities.Length; j++)
            {
                foreach (var v in _spawners[i].templateMesh.vertices)
                {
                    var fly = EntityManager.CreateEntity(_flyArchetype);
                    EntityManager.SetComponentData(fly, new Position { Value = v });
                }

                // Remove the spawner component from the entity.
                EntityManager.RemoveComponent(entities[j], typeof(FlySpawner));
            }

            entities.Dispose();
        }

        _spawners.Clear();
    }
}
