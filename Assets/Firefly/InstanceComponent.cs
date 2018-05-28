using Unity.Entities;

namespace Firefly
{
    [System.Serializable]
    struct Instance : ISharedComponentData
    {
        public UnityEngine.Mesh TemplateMesh;
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Instance")]
    class InstanceComponent : SharedComponentDataWrapper<Instance> {}
}
