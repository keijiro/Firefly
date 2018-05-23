using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct FlyRenderer : ISharedComponentData
{
    public Material material;
    public ShadowCastingMode castShadows;
    public bool receiveShadows;
}

public class FlyRendererComponent : SharedComponentDataWrapper<FlyRenderer>
{
}
