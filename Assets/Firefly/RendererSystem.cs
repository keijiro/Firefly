using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    class RendererSystem : ComponentSystem
    {
        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _dependency; // Just used to enable dependency tracking
        int [] _indexArray = new int [Renderer.MaxVertices];

        protected override void OnCreateManager(int capacity)
        {
            _dependency = GetComponentGroup(typeof(Particle), typeof(Renderer));

            // Default index array
            for (var i = 0; i < Renderer.MaxVertices; i++) _indexArray[i] = i;
        }

        unsafe protected override void OnUpdate()
        {
            var identityMatrix = UnityEngine.Matrix4x4.identity;

            // Iterate over the renderer components.
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);
            foreach (var renderer in _renderers)
            {
                var mesh = renderer.WorkMesh;

                // Do nothing if no mesh (== default empty data)
                if (mesh == null) continue;

                // Check if the mesh has been already used.
                var meshIsReady = (mesh.vertexCount > 0);

                if (!meshIsReady)
                {
                    // Mesh initial settings: 32-bit index, dynamically updated
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.MarkDynamic();
                }

                // Clear the unused part of the vertex buffer.
                var vertexCount = renderer.Counter.Count * 3;
                UnsafeUtility.MemClear(
                    UnsafeUtility.AddressOf(ref renderer.Vertices[vertexCount]),
                    sizeof(float3) * (Renderer.MaxVertices - vertexCount)
                );

                // Update the vertex/normal array via the managed buffers.
                mesh.vertices = renderer.Vertices;
                mesh.normals = renderer.Normals;

                if (!meshIsReady)
                {
                    // Set the default index array for the first time.
                    mesh.triangles = _indexArray;

                    // Set a big bounding box to avoid being culled.
                    mesh.bounds = new UnityEngine.Bounds(
                        UnityEngine.Vector3.zero, UnityEngine.Vector3.one * 1000
                    );
                }

                // Draw call
                UnityEngine.Graphics.DrawMesh(
                    mesh, identityMatrix, renderer.Settings.Material, 0
                );
            }

            _renderers.Clear();
        }
    }
}
