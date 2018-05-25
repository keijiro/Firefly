using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

[System.Serializable]
public struct FlyRenderSettings : ISharedComponentData
{
    public UnityEngine.Material material;
    public UnityEngine.Rendering.ShadowCastingMode castShadows;
    public bool receiveShadows;
}

public class FlyRenderSettingsComponent : SharedComponentDataWrapper<FlyRenderSettings> {}
