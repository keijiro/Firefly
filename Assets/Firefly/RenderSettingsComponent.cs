using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Firefly
{
    [System.Serializable]
    public struct RenderSettings : ISharedComponentData
    {
        public UnityEngine.Material material;
        public UnityEngine.Rendering.ShadowCastingMode castShadows;
        public bool receiveShadows;
    }

    public class RenderSettingsComponent : SharedComponentDataWrapper<RenderSettings> {}
}
