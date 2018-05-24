using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

[System.Serializable]
public struct FlyRenderer : ISharedComponentData
{
    public UnityEngine.Material material;
    public UnityEngine.Rendering.ShadowCastingMode castShadows;
    public bool receiveShadows;
}

public class FlyRendererComponent : SharedComponentDataWrapper<FlyRenderer> {}
