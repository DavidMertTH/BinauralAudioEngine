using System;
using Code.Core;
using Code.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Renderer
{
    /// <summary>
    ///     Computes impulse responses for a set of audio sources based on previously simulated audio paths and a set of
    ///     head-related impulse responses loaded from a SOFA file
    /// </summary>
    public class ImpulseResponseRenderer
    {
    //     /// <summary>
    //     ///     Contiguous array of all impulse responses, ordered by audio source, side (left then right), and time.
    //     ///     The length is determined by the number of sources and the impulse response length defined in
    //     ///     <c>BinauralAudioSettings</c>.
    //     /// </summary>
    //     public NativeArray<float> AllImpulseResponses => _allImpulseResponses;
    //
    //     private NativeArray<float> _allImpulseResponses;
    //
    //     private HeadRelatedImpulseResponses _hrirs;
    //     private readonly float4x4 _listenerWorldToLocal;
    //     private readonly int _irSamplesPerResponse;
    //     private readonly int _irSamplesPerSec;
    //
    //     public ImpulseResponseRenderer(BinauralAudioSettings settings, Transform listener, int numSources)
    //     {
    //         _hrirs = SofaReader.Read(settings.SofaFile);
    //         _irSamplesPerResponse = settings.ImpulseResponseSamples;
    //         _irSamplesPerSec = settings.ImpulseResponseSamplesPerSecond;
    //         _listenerWorldToLocal = listener.worldToLocalMatrix;
    //         _allImpulseResponses =
    //             new NativeArray<float>(_irSamplesPerResponse * numSources * 2, Allocator.Persistent);
    //     }
    //
    //     public JobHandle Schedule(NativeArray<AudioPath>.ReadOnly paths, JobHandle pathsReadyHandle,
    //         out NativeArray<float>.ReadOnly impulseResponses)
    //     {
    //         var computeHandle = new ComputeJob
    //         {
    //             ImpulseResponses = _allImpulseResponses,
    //             Paths = paths,
    //             ListenerWorldToLocal = _listenerWorldToLocal,
    //             IrSamplesPerResponse = _irSamplesPerResponse,
    //             IrSamplesPerSec = _irSamplesPerSec,
    //             Hrirs = _hrirs
    //         }.ScheduleParallel(paths.Length, 32, pathsReadyHandle);
    //         impulseResponses = _allImpulseResponses.AsReadOnly();
    //         return computeHandle;
    //     }
    //
    //     /// <summary>
    //     ///     Get the left and right impulse responses for a particular source
    //     /// </summary>
    //     /// <param name="sourceIndex">The index of the source in the list that was passed to <c>Init</c>.</param>
    //     /// <returns></returns>
    //     public AudioSourceImpulseResponse GetImpulseResponse(int sourceIndex)
    //     {
    //         var leftStartIndex = _irSamplesPerResponse * sourceIndex * 2;
    //         var rightStartIndex = leftStartIndex + _irSamplesPerResponse;
    //         return new AudioSourceImpulseResponse(
    //             _allImpulseResponses.GetSubArray(leftStartIndex, _irSamplesPerResponse).ToArray(),
    //             _allImpulseResponses.GetSubArray(rightStartIndex, _irSamplesPerResponse).ToArray());
    //     }
    //
    //     [BurstCompile]
    //     private struct ComputeJob : IJobFor
    //     {
    //         [NativeDisableParallelForRestriction] public NativeArray<float> ImpulseResponses;
    //
    //         [ReadOnly] public NativeArray<AudioPath>.ReadOnly Paths;
    //         [ReadOnly] public float4x4 ListenerWorldToLocal;
    //         [ReadOnly] public int IrSamplesPerResponse;
    //         [ReadOnly] public float IrSamplesPerSec;
    //         [ReadOnly] public HeadRelatedImpulseResponses Hrirs;
    //
    //         public void Execute(int index)
    //         {
    //             var path = Paths[index];
    //             if (!path.IsValid) return;
    //             if(Mathf.Approximately(path.DistanceToImage, 1))return;
    //             var imagePosInListenerSpace = math.mul(ListenerWorldToLocal, new float4(path.ImagePosition, 1)).xyz;
    //             var bestHrirIndex = FindBestHrir(imagePosInListenerSpace);
    //
    //             var hrirDistance = math.length(Hrirs.Positions[bestHrirIndex]);
    //             var distanceToSource = path.DistanceToImage + hrirDistance;
    //             const float speedOfSound = 343f;
    //             var propagationDelaySec = distanceToSource / speedOfSound;
    //             var propagationDelaySamples = IrSamplesPerSec * propagationDelaySec;
    //
    //             float distanceAmplitude = 3* path.Energy / (path.DistanceToImage * path.DistanceToImage);
    //
    //             var irStartLeft = path.SourceIndex * IrSamplesPerResponse * 2;
    //             var irStartRight = irStartLeft + IrSamplesPerResponse;
    //             var hrirStartLeft = bestHrirIndex * Hrirs.Stride * 2;
    //             var hrirStartRight = hrirStartLeft + Hrirs.Stride;
    //             // ImpulseResponses[irStartLeft + (int)propagationDelaySamples] = distanceAmplitude / 500f;
    //             // ImpulseResponses[irStartRight  + (int)propagationDelaySamples] = distanceAmplitude / 500f;
    //             for (var i = 0; i < Hrirs.Stride && i + propagationDelaySamples < IrSamplesPerResponse; i++)
    //             {
    //                 ImpulseResponses[irStartLeft + i + (int)propagationDelaySamples] +=
    //                 Hrirs.ImpulseResponses[hrirStartLeft + i] * distanceAmplitude;
    //                 ImpulseResponses[irStartRight + i + (int)propagationDelaySamples] +=
    //                 Hrirs.ImpulseResponses[hrirStartRight + i] * distanceAmplitude;
    //             }
    //         }
    //
    //         private int FindBestHrir(float3 sourcePos)
    //         {
    //             var sourceDir = math.normalize(sourcePos);
    //             var bestIndex = -1;
    //             var highestDot = -1f;
    //             for (var i = 0; i < Hrirs.Positions.Length; i++)
    //             {
    //                 var dot = math.dot(sourceDir, math.normalize(Hrirs.Positions[i]));
    //                 if (dot > highestDot)
    //                 {
    //                     highestDot = dot;
    //                     bestIndex = i;
    //                 }
    //             }
    //
    //             return bestIndex;
    //         }
    //     }
    //
    //     public void Dispose()
    //     {
    //         _allImpulseResponses.Dispose();
    //         _hrirs.Dispose();
    //     }
    // }
    //
    // public class AudioSourceImpulseResponse
    // {
    //     public readonly float[] Left;
    //     public readonly float[] Right;
    //
    //     public AudioSourceImpulseResponse(float[] left, float[] right)
    //     {
    //         Left = left;
    //         Right = right;
    //     }
    }
}