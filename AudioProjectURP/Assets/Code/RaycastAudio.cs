using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Code
{
    public class RaycastAudio : MonoBehaviour
    {
        private NativeArray<RaycastCommand>[] _previousCommands;
        private NativeArray<RaycastCommand> _fromTarget;
        private NativeArray<RaycastCommand> _reflectionCommands;

        private NativeArray<RaycastHit> _previousHits;
        private NativeArray<RaycastHit> _targetHits;

        private NativeArray<AudioRay>[] _audioRays;

        public List<AudioRay> GetHighOrderRays(Vector3 target, int bounceAmount,
            NativeArray<RaycastCommand> initialCommands)
        {
            if (bounceAmount <= 0)
            {
                initialCommands.Dispose();
                return null;
            }

            _audioRays = new NativeArray<AudioRay>[bounceAmount];
            _previousCommands = new NativeArray<RaycastCommand>[bounceAmount];

            List<AudioRay> rays = new List<AudioRay>();

            _previousCommands[0] = initialCommands;
            _previousHits = new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);
            _targetHits = new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);

            JobHandle jobHandle = RaycastCommand.ScheduleBatch(initialCommands, _previousHits, 1, 1);
            jobHandle.Complete();

            _fromTarget = new NativeArray<RaycastCommand>(_previousHits.Length, Allocator.TempJob);
            _reflectionCommands = new NativeArray<RaycastCommand>(_previousHits.Length, Allocator.TempJob);

            for (int i = 0; i < bounceAmount; i++)
            {
                _audioRays[i] = new NativeArray<AudioRay>(_previousHits.Length, Allocator.TempJob);

                if (i > 0)
                {
                    _previousCommands[i] = new NativeArray<RaycastCommand>(_reflectionCommands, Allocator.TempJob);
                }

                FillRays fillJob = new FillRays()
                {
                    PreviousHit = _previousHits,
                    Target = target,
                    AudioRays = _audioRays[i],
                    ReflectionRay = _reflectionCommands,
                    FromTarget = _fromTarget,
                    PreviousRay = _previousCommands[i]
                };
                JobHandle fillHandle = fillJob.Schedule(initialCommands.Length, 8);
                fillHandle.Complete();
                JobHandle toTargetHandle = RaycastCommand.ScheduleBatch(_fromTarget, _targetHits, 1, 1);
                toTargetHandle.Complete();

                EvalRays evalJob = new EvalRays()
                {
                    PreviousHits = _previousHits,
                    AudioRays = _audioRays[i],
                    CurrentHits = _targetHits,
                };
                JobHandle evalHandle = evalJob.Schedule(initialCommands.Length, 8);
                evalHandle.Complete();
                rays.AddRange(GetRayList(_audioRays[i]));
                JobHandle reflectionHandle = RaycastCommand.ScheduleBatch(_reflectionCommands, _previousHits, 1, 1);
                reflectionHandle.Complete();
            }

            for (int i = 0; i < bounceAmount; i++)
            {
                _audioRays[i].Dispose();
                _previousCommands[i].Dispose();
            }

            _previousHits.Dispose();
            _targetHits.Dispose();
            _reflectionCommands.Dispose();
            _fromTarget.Dispose();
            return rays;
        }

        private void OnDestroy()
        {
            if (_fromTarget.IsCreated) _fromTarget.Dispose();
            if (_reflectionCommands.IsCreated) _reflectionCommands.Dispose();
            if (_previousHits.IsCreated) _previousHits.Dispose();
            if (_targetHits.IsCreated) _targetHits.Dispose();
            for (int i = 0; i < _audioRays.Length; i++)
            {
                if (_audioRays[i].IsCreated) _audioRays[i].Dispose();
                if (_audioRays[i].IsCreated) _previousCommands[i].Dispose();
            }
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

                if (AudioRays[index].Absorbtion <= 0.00001f)
                {
                    ray.Absorbtion = 0.3f;
                }
                else
                {
                    ray.Absorbtion = AudioRays[index].Absorbtion * 0.8f;
                }
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