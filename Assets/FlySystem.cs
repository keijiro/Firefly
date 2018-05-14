using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

public class FlySystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct FlyUpdateJob : IJobProcessComponentData<Fly, Position>
    {
        public float dt;

        public void Execute(ref Fly fly, [ReadOnly] ref Position pos)
        {
            fly.Life += dt;
        }
    }

    protected override JobHandle OnUpdate(JobHandle deps)
    {
        var job = new FlyUpdateJob() { dt = UnityEngine.Time.time };
        return job.Schedule(this, 64, deps);
    }
}
