using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Firefly
{
    public class RendererSystem : ComponentSystem
    {
        List<Renderer> _renderers = new List<Renderer>();
        ComponentGroup _dependency; // used just for dependency tracking

        UnityEngine.Vector3[] _managedVertexArray;
        UnityEngine.Vector3[] _managedNormalArray;
        int[] _managedIndexArray;

        protected override void OnCreateManager(int capacity)
        {
            _dependency = GetComponentGroup(typeof(Disintegrator), typeof(Renderer));

            _managedVertexArray = new UnityEngine.Vector3[Renderer.kMaxVertices];
            _managedNormalArray = new UnityEngine.Vector3[Renderer.kMaxVertices];
            _managedIndexArray = new int[Renderer.kMaxVertices];

            for (var i = 0; i < Renderer.kMaxVertices; i++) _managedIndexArray[i] = i;
        }

        protected override void OnDestroyManager()
        {
            _managedVertexArray = null;
            _managedNormalArray = null;
            _managedIndexArray = null;
        }

        unsafe protected override void OnUpdate()
        {
            EntityManager.GetAllUniqueSharedComponentDatas(_renderers);

            var matrix = UnityEngine.Matrix4x4.identity;
            var copySize = sizeof(float3) * Renderer.kMaxVertices;

            var pVArray = UnsafeUtility.AddressOf(ref _managedVertexArray[0]);
            var pNArray = UnsafeUtility.AddressOf(ref _managedNormalArray[0]);

            foreach (var renderer in _renderers)
            {
                if (renderer.WorkMesh == null) continue;

                var meshIsReady = (renderer.WorkMesh.vertexCount > 0);

                if (!meshIsReady)
                {
                    renderer.WorkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    renderer.WorkMesh.MarkDynamic();
                }

                UnsafeUtility.MemCpy(pVArray, renderer.Vertices.GetUnsafePtr(), copySize);
                UnsafeUtility.MemCpy(pNArray, renderer.Normals.GetUnsafePtr(), copySize);

                renderer.WorkMesh.vertices = _managedVertexArray;
                renderer.WorkMesh.normals = _managedNormalArray;

                if (!meshIsReady)
                {
                    renderer.WorkMesh.triangles = _managedIndexArray;
                    renderer.WorkMesh.bounds = new UnityEngine.Bounds(
                        UnityEngine.Vector3.zero, UnityEngine.Vector3.one * 1000
                    );
                }

                UnityEngine.Graphics.DrawMesh(
                    renderer.WorkMesh, matrix, renderer.Settings.material, 0
                );
            }

            _renderers.Clear();
        }
    }
}
