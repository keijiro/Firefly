using Unity.Entities;
using UnityEngine;

namespace Firefly
{
    [System.Serializable]
    struct NoiseEffector : IComponentData
    {
        public float Frequency;
        public float Amplitude;
    }

    [UnityEngine.AddComponentMenu("Firefly/Firefly Noise Effector")]
    sealed class NoiseEffectorComponent : ComponentDataWrapper<NoiseEffector>
    {
        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
