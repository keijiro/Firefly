using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Firefly
{
    [System.Serializable]
    public struct RenderSettings : ISharedComponentData
    {
        public UnityEngine.Material Material;
        public UnityEngine.Rendering.ShadowCastingMode CastShadows;
        public bool ReceiveShadows;
    }

    public class RenderSettingsComponent : SharedComponentDataWrapper<RenderSettings> {}
}
