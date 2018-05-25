using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

class FlySpawnSystem : ComponentSystem
{
    // Used to enumerate spawn components
    List<FlySpawn> _spawnDatas = new List<FlySpawn>();
    ComponentGroup _spawnGroup;

    // Fly entity archetype used for instantiation
    EntityArchetype _flyArchetype;

    // Allocation list
    List<FlyRenderer> _toBeDisposed = new List<FlyRenderer>();

    protected override void OnCreateManager(int capacity)
    {
        _spawnGroup = GetComponentGroup(
            typeof(Position), typeof(FlySpawn), typeof(FlyRenderSettings)
        );

        _flyArchetype = EntityManager.CreateArchetype(
            typeof(Fly), typeof(Facet), typeof(Position), typeof(FlyRenderer)
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
        // Enumerate all the spawn data entries.
        EntityManager.GetAllUniqueSharedComponentDatas(_spawnDatas);
        foreach (var spawnData in _spawnDatas)
        {
            // Skip if it has no data.
            if (spawnData.templateMesh == null) continue;

            // Get a copy of the spawn entity array.
            // Don't directly use the iterator -- we're going to remove
            // the spawn components, and it will invalidate the iterator.
            _spawnGroup.SetFilter(spawnData);
            var iterator = _spawnGroup.GetEntityArray();
            if (iterator.Length == 0) continue;
            var spawnEntities = new NativeArray<Entity>(iterator.Length, Allocator.Temp);
            iterator.CopyTo(spawnEntities);

            // Retrieve the mesh data.
            var vertices = spawnData.templateMesh.vertices;
            var indices = spawnData.templateMesh.triangles;

            // Instantiate flies along with the spawn entities.
            for (var j = 0; j < spawnEntities.Length; j++)
            {
                // Retrieve the source data.
                var spawnEntity = spawnEntities[j];
                var position = EntityManager.GetComponentData<Position>(spawnEntity).Value;

                // Create a renderer for this group.
                var renderer = new FlyRenderer();
                renderer.Settings = EntityManager.GetSharedComponentData<FlyRenderSettings>(spawnEntity);
                renderer.Vertices = new NativeArray<float3>(FlyRenderer.kMaxVertices, Allocator.Persistent);
                renderer.Normals = new NativeArray<float3>(FlyRenderer.kMaxVertices, Allocator.Persistent);
                renderer.MeshInstance = new UnityEngine.Mesh();
                renderer.Counter = new NativeCounter(Allocator.Persistent);

                _toBeDisposed.Add(renderer);

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

                // Remove the spawn component from the entity.
                EntityManager.RemoveComponent(spawnEntity, typeof(FlySpawn));
            }

            spawnEntities.Dispose();
        }

        _spawnDatas.Clear();
    }
}
