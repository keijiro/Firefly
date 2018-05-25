using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct Instance : ISharedComponentData
    {
        public UnityEngine.Mesh templateMesh;
    }

    class InstanceComponent : SharedComponentDataWrapper<Instance> {}
}
