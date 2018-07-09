using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Firefly
{
    [Unity.Burst.BurstCompile]
    unsafe struct SimpleReconstructionJob :
        IJobParallelFor, IParticleReconstructionJob<SimpleParticle>
    {
        [ReadOnly] ComponentDataArray<Particle> _particles;
        [ReadOnly] ComponentDataArray<Position> _positions;
        [ReadOnly] ComponentDataArray<Triangle> _triangles;

        [NativeDisableUnsafePtrRestriction] void* _vertices;
        [NativeDisableUnsafePtrRestriction] void* _normals;

        SimpleParticle _variant;
        NativeCounter.Concurrent _counter;

        public void Initialize(
            SimpleParticle variant,
            ComponentGroup group,
            UnityEngine.Vector3 [] vertices,
            UnityEngine.Vector3 [] normals,
            NativeCounter.Concurrent counter
        )
        {
            _particles = group.GetComponentDataArray<Particle>();
            _positions = group.GetComponentDataArray<Position>();
            _triangles = group.GetComponentDataArray<Triangle>();

            _vertices = UnsafeUtility.AddressOf(ref vertices[0]);
            _normals = UnsafeUtility.AddressOf(ref normals[0]);

            _variant = variant;
            _counter = counter;
        }

        public void Execute(int index)
        {
            var particle = _particles[index];
            var face = _triangles[index];

            // Scaling with simple lerp
            var scale = 1 - particle.Time / (_variant.Life * particle.LifeRandom);

            // Random rotation
            var fwd = particle.Velocity + 1e-4f;
            var axis = math.normalize(math.cross(fwd, face.Vertex1));
            var avel = Random.Value01(particle.ID + 10000) * 8;
            var rot = quaternion.axisAngle(axis, particle.Time * avel);

            // Vertex positions
            var pos = _positions[index].Value;
            var v1 = pos + math.mul(rot, face.Vertex1) * scale;
            var v2 = pos + math.mul(rot, face.Vertex2) * scale;
            var v3 = pos + math.mul(rot, face.Vertex3) * scale;

            // Vertex output
            var i = _counter.Increment() * 3;
            UnsafeUtility.WriteArrayElement(_vertices, i + 0, v1);
            UnsafeUtility.WriteArrayElement(_vertices, i + 1, v2);
            UnsafeUtility.WriteArrayElement(_vertices, i + 2, v3);

            // Normal output
            var n = math.normalize(math.cross(v2 - v1, v3 - v1));
            UnsafeUtility.WriteArrayElement(_normals, i + 0, n);
            UnsafeUtility.WriteArrayElement(_normals, i + 1, n);
            UnsafeUtility.WriteArrayElement(_normals, i + 2, n);
        }
    }

    sealed class SimpleParticleExpirationSystem :
        ParticleExpirationSystemBase<SimpleParticle> {}

    sealed class SimpleParticleReconstructionSystem :
        ParticleReconstructionSystemBase<SimpleParticle, SimpleReconstructionJob> {}
}
