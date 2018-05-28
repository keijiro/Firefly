using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct Instance : ISharedComponentData
    {
        public UnityEngine.Mesh TemplateMesh;
    }

    class InstanceComponent : SharedComponentDataWrapper<Instance> {}
}
