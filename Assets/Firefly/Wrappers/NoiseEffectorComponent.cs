using UnityEngine;

namespace Firefly
{
    [AddComponentMenu("Firefly/Firefly Noise Effector")]
    sealed class NoiseEffectorComponent :
        Unity.Entities.ComponentDataWrapper<NoiseEffector>
    {
        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
