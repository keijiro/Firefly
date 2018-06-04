using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Firefly
{
    [ComputeJobOptimization]
    unsafe struct SimpleReconstructionJob :
        IJobParallelFor, IParticleReconstructionJob
    {
        [ReadOnly] public ComponentDataArray<Particle> Particles;
        [ReadOnly] public ComponentDataArray<Position> Positions;
        [ReadOnly] public ComponentDataArray<Triangle> Triangles;
        [ReadOnly] public SharedComponentDataArray<SimpleParticle> Variants;

        [NativeDisableUnsafePtrRestriction] public void* Vertices;
        [NativeDisableUnsafePtrRestriction] public void* Normals;

        public NativeCounter.Concurrent Counter;

        public void Initialize(
            ComponentGroup group,
            UnityEngine.Vector3 [] vertices,
            UnityEngine.Vector3 [] normals,
            NativeCounter.Concurrent counter
        )
        {
            Particles = group.GetComponentDataArray<Particle>();
            Positions = group.GetComponentDataArray<Position>();
            Triangles = group.GetComponentDataArray<Triangle>();
            Variants = group.GetSharedComponentDataArray<SimpleParticle>();
            Vertices = UnsafeUtility.AddressOf(ref vertices[0]);
            Normals = UnsafeUtility.AddressOf(ref normals[0]);
            Counter = counter;
        }

        public void Execute(int index)
        {
            var particle = Particles[index];
            var face = Triangles[index];

            var fwd = particle.Velocity + 1e-4f;
            var axis = math.normalize(math.cross(fwd, face.Vertex1));
            var rot = math.axisAngle(axis, particle.Time * 3);

            var pos = Positions[index].Value;
            var v1 = pos + math.mul(rot, face.Vertex1);
            var v2 = pos + math.mul(rot, face.Vertex2);
            var v3 = pos + math.mul(rot, face.Vertex3);

            var i = Counter.Increment() * 3;
            UnsafeUtility.WriteArrayElement(Vertices, i + 0, v1);
            UnsafeUtility.WriteArrayElement(Vertices, i + 1, v2);
            UnsafeUtility.WriteArrayElement(Vertices, i + 2, v3);

            var n = math.normalize(math.cross(v2 - v1, v3 - v1));
            UnsafeUtility.WriteArrayElement(Normals, i + 0, n);
            UnsafeUtility.WriteArrayElement(Normals, i + 1, n);
            UnsafeUtility.WriteArrayElement(Normals, i + 2, n);
        }
    }

    sealed class SimpleParticleExpirationSystem :
        ParticleExpirationSystemBase<SimpleParticle> {}

    sealed class SimpleParticleReconstructionSystem :
        ParticleReconstructionSystemBase<SimpleParticle, SimpleReconstructionJob> {}
}
