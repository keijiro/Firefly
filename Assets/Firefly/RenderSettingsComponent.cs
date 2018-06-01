using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Firefly
{
    [System.Serializable]
    struct RenderSettings : ISharedComponentData
    {
        public UnityEngine.Material Material;
        public UnityEngine.Rendering.ShadowCastingMode CastShadows;
        public bool ReceiveShadows;
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Render Settings")]
    sealed class RenderSettingsComponent : SharedComponentDataWrapper<RenderSettings> {}
}
