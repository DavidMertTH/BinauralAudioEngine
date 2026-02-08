using Code.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Renderer
{
    public static class ComputeImpulseResponse
    {
        public static JobHandle Schedule(NativeArray<AudioPath>.ReadOnly paths, Transform listener, int irSamples,
            float irSamplesPerSec, HeadRelatedImpulseResponses hrirs, JobHandle pathsReadyHandle,
            NativeArray<float> result)
        {
            var computeHandle = new ComputeJob()
            {
                ImpulseResponses = result,
                Paths = paths,
                ListenerWorldToLocal = listener.worldToLocalMatrix,
                IrSamplesPerResponse = irSamples,
                IrSamplesPerSec = irSamplesPerSec,
                Hrirs = hrirs
            }.ScheduleParallel(paths.Length, 32, pathsReadyHandle);
            return computeHandle;
        }

        [BurstCompile]
        private struct ComputeJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<float> ImpulseResponses;

            [ReadOnly] public NativeArray<AudioPath>.ReadOnly Paths;
            [ReadOnly] public float4x4 ListenerWorldToLocal;
            [ReadOnly] public int IrSamplesPerResponse;
            [ReadOnly] public float IrSamplesPerSec;
            [ReadOnly] public HeadRelatedImpulseResponses Hrirs;

            public void Execute(int index)
            {
                // TODO: Account for HRIR sampling rate
                var path = Paths[index];
                if (!path.IsValid) return;

                var imagePosInListenerSpace = math.mul(ListenerWorldToLocal, new float4(path.ImagePosition, 1)).xyz;
                var bestHrirIndex = FindBestHrir(imagePosInListenerSpace);

                // The distance from the center of the head to the HRTF measurement point.
                var hrirDistance = math.length(Hrirs.Positions[bestHrirIndex]);

                // TODO: Should hrirDistance be subtracyted instead?
                float distanceToSource = path.DistanceToImage + hrirDistance;
                const float speedOfSound = 343f;
                float propagationDelaySec = distanceToSource / speedOfSound;
                float propagationDelaySamples = IrSamplesPerSec * propagationDelaySec;
                float distanceAmplitudeTwo = path.Energy * (8 / distanceToSource);

                var irStartLeft = path.SourceIndex * IrSamplesPerResponse * 2;
                var irStartRight = irStartLeft + IrSamplesPerResponse;
                var hrirStartLeft = bestHrirIndex * Hrirs.Stride * 2;
                var hrirStartRight = hrirStartLeft + Hrirs.Stride;

                for (int i = 0; i < Hrirs.Stride && i + propagationDelaySamples < IrSamplesPerResponse; i++)
                {
                    ImpulseResponses[irStartLeft + i + (int)propagationDelaySamples] +=
                        Hrirs.ImpulseResponses[hrirStartLeft + i] * distanceAmplitudeTwo;
                    ImpulseResponses[irStartRight + i + (int)propagationDelaySamples] +=
                        Hrirs.ImpulseResponses[hrirStartRight + i] * distanceAmplitudeTwo;
                }
            }

            private int FindBestHrir(float3 sourcePos)
            {
                var sourceDir = math.normalize(sourcePos);
                var bestIndex = -1;
                var highestDot = -1f;
                for (var i = 0; i < Hrirs.Positions.Length; i++)
                {
                    var dot = math.dot(sourceDir, math.normalize(Hrirs.Positions[i]));
                    if (dot > highestDot)
                    {
                        highestDot = dot;
                        bestIndex = i;
                    }
                }

                return bestIndex;
            }
        }
    }
}