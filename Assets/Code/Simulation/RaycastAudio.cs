using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class RaycastAudio : MonoBehaviour
    {
        private NativeArray<RaycastCommand>[] _previousCommands;
        private NativeArray<RaycastCommand> _fromTarget;
        private NativeArray<RaycastCommand> _reflectionCommands;

        private NativeArray<RaycastHit> _previousHits;
        private NativeArray<RaycastHit> _targetHits;

        private NativeArray<AudioPath>[] _audioPathsToTarget;
        private NativeArray<AudioPath> _audioRaysContinuous;

        public List<AudioPath> GetHighOrderRays(Vector3 target, int bounceAmount,
            NativeArray<RaycastCommand> initialCommands, float absorbtion)
        {
            if (bounceAmount <= 0)
            {
                initialCommands.Dispose();
                return null;
            }

            _audioPathsToTarget = new NativeArray<AudioPath>[bounceAmount];
            _previousCommands = new NativeArray<RaycastCommand>[bounceAmount];

            List<AudioPath> rays = new List<AudioPath>();

            _previousCommands[0] = initialCommands;
            _previousHits = new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);
            _targetHits = new NativeArray<RaycastHit>(initialCommands.Length, Allocator.TempJob);
            _audioRaysContinuous = new NativeArray<AudioPath>(initialCommands.Length, Allocator.TempJob);

            JobHandle jobHandle = RaycastCommand.ScheduleBatch(initialCommands, _previousHits, 1, 1);
            jobHandle.Complete();

            _fromTarget = new NativeArray<RaycastCommand>(_previousHits.Length, Allocator.TempJob);
            _reflectionCommands = new NativeArray<RaycastCommand>(_previousHits.Length, Allocator.TempJob);

            for (int i = 0; i < bounceAmount; i++)
            {
                _audioPathsToTarget[i] = new NativeArray<AudioPath>(_previousHits.Length, Allocator.TempJob);

                if (i > 0)
                {
                    _previousCommands[i] = new NativeArray<RaycastCommand>(_reflectionCommands, Allocator.TempJob);
                }

                FillRays fillJob = new FillRays()
                {
                    PreviousHit = _previousHits,
                    Target = target,
                    AudioRays = _audioRaysContinuous,
                    AudioRaysToTarget = _audioPathsToTarget[i],
                    ReflectionRay = _reflectionCommands,
                    FromTarget = _fromTarget,
                    PreviousRay = _previousCommands[i],
                    Absorbtion = absorbtion
                };
                JobHandle fillHandle = fillJob.Schedule(initialCommands.Length, 8);
                JobHandle toTargetHandle = RaycastCommand.ScheduleBatch(_fromTarget, _targetHits, 1, 1, fillHandle);
                EvalRays evalJob = new EvalRays()
                {
                    PreviousHits = _previousHits,
                    AudioRays = _audioPathsToTarget[i],
                    ToTarget = _targetHits,
                    Target = target,
                };
                JobHandle evalHandle = evalJob.Schedule(initialCommands.Length, 1, toTargetHandle);
                //JobHandle evalHandle = evalJob.Schedule(initialCommands.Length, 8);
                evalHandle.Complete();
                rays.AddRange(GetRayList(_audioPathsToTarget[i]));
                JobHandle reflectionHandle = RaycastCommand.ScheduleBatch(_reflectionCommands, _previousHits, 1, 1);
                reflectionHandle.Complete();
            }

            for (int i = 0; i < bounceAmount; i++)
            {
                _audioPathsToTarget[i].Dispose();
                _previousCommands[i].Dispose();
            }

            _previousHits.Dispose();
            _targetHits.Dispose();
            _reflectionCommands.Dispose();
            _fromTarget.Dispose();
            _audioRaysContinuous.Dispose();
            return rays;
        }

        private void OnDestroy()
        {
            if (_fromTarget.IsCreated) _fromTarget.Dispose();
            if (_reflectionCommands.IsCreated) _reflectionCommands.Dispose();
            if (_previousHits.IsCreated) _previousHits.Dispose();
            if (_targetHits.IsCreated) _targetHits.Dispose();
            if (_audioPathsToTarget != null)
            {
                for (int i = 0; i < _audioPathsToTarget.Length; i++)
                {
                    if (_audioPathsToTarget[i].IsCreated) _audioPathsToTarget[i].Dispose();
                    if (_audioPathsToTarget[i].IsCreated) _previousCommands[i].Dispose();
                }
            }
        }

        private AudioPath[] GetRayList(NativeArray<AudioPath> audioRays)
        {
            AudioPath[] rays = new AudioPath[audioRays.Length];
            audioRays.CopyTo(rays);
            return rays;
        }

        [BurstCompile]
        private struct EvalRays : IJobParallelFor
        {
            public NativeArray<AudioPath> AudioRays;
            
            [ReadOnly] public NativeArray<RaycastHit> PreviousHits;
            [ReadOnly] public NativeArray<RaycastHit> ToTarget;
            [ReadOnly] public float3 Target;
            [ReadOnly] public float ScatteringCoefficient;
            public void Execute(int index)
            {
                if (ToTarget[index].distance < 0.001f || AudioRays[index].Reflections <= 2)
                {
                    AudioRays[index] = new AudioPath() { IsValid = false };
                    return;
                }

                if ((PreviousHits[index].point - ToTarget[index].point).magnitude < 0.01f)
                {
                    AudioPath path = AudioRays[index];
                    
                    path.IsValid = true;
                    path.ImagePosition = PreviousHits[index].point;
                    
                    float angleRadians = math.radians(path.ScatteringDivergence);

                    float contribution = (1 - ScatteringCoefficient) + ScatteringCoefficient * math.max(0f, math.cos(angleRadians));

                    
                    path.DistanceToImage += math.distance(Target, ToTarget[index].point);
                    path.Energy *= contribution;
                    AudioRays[index] = path;
                    if (path.DistanceToImage < 9.28f)
                    {
                        AudioRays[index] = path;
                    }
                }
            }
        }

        [BurstCompile]
        private struct FillRays : IJobParallelFor
        {
            public NativeArray<AudioPath> AudioRaysToTarget;
            public NativeArray<AudioPath> AudioRays;

            public NativeArray<RaycastCommand> ReflectionRay;
            public NativeArray<RaycastCommand> FromTarget;
            

            [ReadOnly] public NativeArray<RaycastHit> PreviousHit;
            [ReadOnly] public NativeArray<RaycastCommand> PreviousRay;
            [ReadOnly] public int WriteIndex;
            [ReadOnly] public Vector3 Target;
            [ReadOnly] public float Absorbtion;

            public void Execute(int index)
            {
                if (PreviousHit[index].distance < 0.0001f) return;
                var ray = AudioRays[index];

                if (ray.Energy <= 0.000001f)
                {
                    ray.Energy = 0.9f;
                }
                else
                {
                    ray.Energy = AudioRays[index].Energy * Absorbtion;
                }

                ray.Reflections = AudioRays[index].Reflections + 1;
                ray.DistanceToImage += PreviousHit[index].distance;
                ray.ImagePosition = PreviousHit[index].point + PreviousHit[index].normal * 0.001f;
                ray.Positions.Add(ray.ImagePosition);

                
                FromTarget[index] = new RaycastCommand(Target, PreviousHit[index].point - Target,
                    QueryParameters.Default);

                Vector3 reflectedDir = PreviousRay[index].direction - 2f *
                    Vector3.Dot(PreviousRay[index].direction, PreviousHit[index].normal) * PreviousHit[index].normal;

                ReflectionRay[index] =
                    new RaycastCommand(PreviousHit[index].point, reflectedDir, QueryParameters.Default);
                
                ray.ScatteringDivergence = Vector3.Angle(reflectedDir, PreviousHit[index].point - Target);
                
                AudioRays[index] = ray;
                AudioRaysToTarget[index] = ray;
            }
        }
    }
}