using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Code
{
    public class RaycastAudio : MonoBehaviour
    {
        public List<AudioRay> GetHighOrderRays(int resolution, Vector3 target, int bounceAmount,
            NativeArray<RaycastCommand> initialCommands)
        {
            List<AudioRay> rays = new List<AudioRay>();

            NativeArray<RaycastCommand> previousCommands = initialCommands;
            NativeArray<RaycastHit> previousHits =
                new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);
            NativeArray<RaycastHit> targetHits = new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);

            JobHandle jobHandle = RaycastCommand.ScheduleBatch(initialCommands, previousHits, 1, 1);
            jobHandle.Complete();

            NativeArray<RaycastCommand>
                fromTarget = new NativeArray<RaycastCommand>(previousHits.Length, Allocator.TempJob);
            NativeArray<RaycastCommand>
                reflectionCommands = new NativeArray<RaycastCommand>(previousHits.Length, Allocator.TempJob);

            for (int i = 0; i < bounceAmount; i++)
            {
                NativeArray<AudioRay> audioRays = new NativeArray<AudioRay>(previousHits.Length, Allocator.TempJob);

                FillRays fillJob = new FillRays()
                {
                    PreviousHit = previousHits,
                    Target = target,
                    AudioRays = audioRays,
                    ReflectionRay = reflectionCommands,
                    FromTarget = fromTarget,
                    PreviousRay = previousCommands
                };
                JobHandle fillHandle = fillJob.Schedule(initialCommands.Length, 8);
                fillHandle.Complete();
                JobHandle toTargetHandle = RaycastCommand.ScheduleBatch(fromTarget, targetHits, 1, 1);
                toTargetHandle.Complete();

                EvalRays evalJob = new EvalRays()
                {
                    PreviousHits = previousHits,
                    AudioRays = audioRays,
                    CurrentHits = targetHits,
                };
                JobHandle evalHandle = evalJob.Schedule(initialCommands.Length, 8);
                evalHandle.Complete();
                rays.AddRange(GetRayList(audioRays));
                JobHandle reflectionHandle = RaycastCommand.ScheduleBatch(reflectionCommands, previousHits, 1, 1);
                reflectionHandle.Complete();
                previousCommands.Dispose();
                previousCommands = new NativeArray<RaycastCommand>(reflectionCommands, Allocator.TempJob);
                audioRays.Dispose();
            }

            previousHits.Dispose();
            targetHits.Dispose();
            reflectionCommands.Dispose();
            fromTarget.Dispose();
            Debug.Log("FOUND: " + rays.Count);
            return rays;
        }

        private List<AudioRay> GetRayList(NativeArray<AudioRay> audioRays)
        {
            List<AudioRay> list = new List<AudioRay>(audioRays.Length);
            int length = audioRays.Length;
            for (int i = 0; i < length; i++)
            {
                AudioRay ray = audioRays[i];
                if (ray.IsValid) list.Add(ray);
            }

            return list;
        }

        [BurstCompile]
        private struct EvalRays : IJobParallelFor
        {
            public NativeArray<AudioRay> AudioRays;

            [ReadOnly] public NativeArray<RaycastHit> PreviousHits;
            [ReadOnly] public NativeArray<RaycastHit> CurrentHits;

            public void Execute(int index)
            {
                if (CurrentHits[index].distance < 0.001f)
                {
                    AudioRays[index] = new AudioRay() { IsValid = false };
                    return;
                }

                if ((PreviousHits[index].point - CurrentHits[index].point).magnitude < 0.01f)
                {
                    AudioRay ray = AudioRays[index];

                    ray.IsValid = true;
                    ray.ImagePosition = PreviousHits[index].point;

                    AudioRays[index] = ray;
                }
            }
        }

        [BurstCompile]
        private struct FillRays : IJobParallelFor
        {
            public NativeArray<AudioRay> AudioRays;

            public NativeArray<RaycastCommand> ReflectionRay;
            public NativeArray<RaycastCommand> FromTarget;

            [ReadOnly] public NativeArray<RaycastHit> PreviousHit;
            [ReadOnly] public NativeArray<RaycastCommand> PreviousRay;

            [ReadOnly] public Vector3 Target;

            public void Execute(int index)
            {
                if (PreviousHit[index].distance < 0.001f) return;
                var ray = AudioRays[index];

                ray.DistanceToImage += PreviousHit[index].distance;
                ray.ImagePosition = PreviousHit[index].point + PreviousHit[index].normal * 0.001f;

                AudioRays[index] = ray;

                FromTarget[index] = new RaycastCommand(Target, PreviousHit[index].point - Target,
                    QueryParameters.Default);

                Vector3 reflectedDir = PreviousRay[index].direction - 2f *
                    Vector3.Dot(PreviousRay[index].direction, PreviousHit[index].normal) * PreviousHit[index].normal;

                ReflectionRay[index] =
                    new RaycastCommand(PreviousHit[index].point, reflectedDir, QueryParameters.Default);
            }
        }
    }
}