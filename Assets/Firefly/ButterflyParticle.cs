using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Firefly
{
    [Unity.Burst.BurstCompile]
    unsafe struct ButterflyReconstructionJob :
        IJobParallelFor, IParticleReconstructionJob<ButterflyParticle>
    {
        [ReadOnly] ComponentDataArray<Particle> _particles;
        [ReadOnly] ComponentDataArray<Position> _positions;
        [ReadOnly] ComponentDataArray<Triangle> _triangles;

        [NativeDisableUnsafePtrRestriction] void* _vertices;
        [NativeDisableUnsafePtrRestriction] void* _normals;

        ButterflyParticle _variant;
        NativeCounter.Concurrent _counter;

        public void Initialize(
            ButterflyParticle variant,
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

        void AddTriangle(float3 v1, float3 v2, float3 v3)
        {
            var i = _counter.Increment() * 3;
            UnsafeUtility.WriteArrayElement(_vertices, i + 0, v1);
            UnsafeUtility.WriteArrayElement(_vertices, i + 1, v2);
            UnsafeUtility.WriteArrayElement(_vertices, i + 2, v3);

            var n = math.normalize(math.cross(v2 - v1, v3 - v1));
            UnsafeUtility.WriteArrayElement(_normals, i + 0, n);
            UnsafeUtility.WriteArrayElement(_normals, i + 1, n);
            UnsafeUtility.WriteArrayElement(_normals, i + 2, n);
        }

        public void Execute(int index)
        {
            var particle = _particles[index];

            // Scaling with simple lerp
            var t_s = particle.Time / (_variant.Life * particle.LifeRandom);
            var size = _variant.Size * (1 - t_s);

            // Look-at matrix from velocity
            var az = particle.Velocity + 0.001f;
            var ax = math.cross(new float3(0, 1, 0), az);
            var ay = math.cross(az, ax);

            // Flapping
            var freq = 8 + Random.Value01(particle.ID + 10000) * 20;
            var flap = math.sin(freq * particle.Time);

            // Axis vectors
            ax = math.normalize(ax) * size;
            ay = math.normalize(ay) * size * flap;
            az = math.normalize(az) * size;

            // Vertices
            var pos = _positions[index].Value;
            var face = _triangles[index];

            var va1 = pos + face.Vertex1;
            var va2 = pos + face.Vertex2;
            var va3 = pos + face.Vertex3;

            var vb1 = pos + az * 0.2f;
            var vb2 = pos - az * 0.2f;
            var vb3 = pos - ax + ay + az;
            var vb4 = pos - ax + ay - az;
            var vb5 = vb3 + ax * 2;
            var vb6 = vb4 + ax * 2;

            var p_t = math.saturate(particle.Time);
            var v1 = math.lerp(va1, vb1, p_t);
            var v2 = math.lerp(va2, vb2, p_t);
            var v3 = math.lerp(va3, vb3, p_t);
            var v4 = math.lerp(va3, vb4, p_t);
            var v5 = math.lerp(va3, vb5, p_t);
            var v6 = math.lerp(va3, vb6, p_t);

            // Output
            AddTriangle(v1, v2, v5);
            AddTriangle(v5, v2, v6);
            AddTriangle(v3, v4, v1);
            AddTriangle(v1, v4, v2);
        }
    }

    sealed class ButterflyParticleExpirationSystem :
        ParticleExpirationSystemBase<ButterflyParticle> {}

    sealed class ButterflyParticleReconstructionSystem :
        ParticleReconstructionSystemBase<ButterflyParticle, ButterflyReconstructionJob> {}
}
