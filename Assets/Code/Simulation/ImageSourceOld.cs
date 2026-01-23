using System;
using System.Collections.Generic;
using Code.Renderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Simulation
{
    public class ImageSourceOld : MonoBehaviour
    {
        public GameObject target;
        public AudioSource source;
        [FormerlySerializedAs("audioTest")] public BinauralAudioProcessor binauralAudioProcessor;
        public bool calculateAcoustics;
        private NativeArray<AudioPath> _primaryReflections;
        private NativeArray<AudioPath> _secondaryReflections;

        private List<AudioPath> _primaryReflectionsList;
        private List<AudioPath> _secondaryReflectionsList;


        private void OnDestroy()
        {
            if (_primaryReflections.IsCreated) _primaryReflections.Dispose();
        }

        private struct RayIdentifier : IEquatable<RayIdentifier>
        {
            public int RoundedDistance;
            public int3 RoundedDirection; // Verwenden wir int3 für besseren Hash-Vergleich

            public bool Equals(RayIdentifier other)
            {
                return RoundedDistance == other.RoundedDistance &&
                       RoundedDirection.Equals(other.RoundedDirection);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + RoundedDistance;
                    hash = hash * 31 + RoundedDirection.GetHashCode();
                    return hash;
                }
            }
        }

        private List<AudioPath> SaveSecondaryReflections(NativeArray<AudioPath> inputPaths)
        {
            var paths = new List<AudioPath>();
            var seen = new HashSet<RayIdentifier>();

            foreach (var ray in inputPaths)
            {
                if (!ray.IsValid) continue;

                float3 direction = math.normalize(ray.ImagePosition - (float3)source.transform.position);

                int3 roundedDir = new int3(
                    (int)math.round(direction.x * 100),
                    (int)math.round(direction.y * 100),
                    (int)math.round(direction.z * 100)
                );

                int roundedDistance = (int)math.round(ray.DistanceToImage * 1000f);

                var id = new RayIdentifier
                {
                    RoundedDistance = roundedDistance,
                    RoundedDirection = roundedDir
                };

                if (seen.Contains(id)) continue;

                seen.Add(id);
                paths.Add(ray);
            }

            return paths;
        }

        private List<AudioPath> SavePrimaryReflections(NativeArray<AudioPath> primaryReflections)
        {
            List<AudioPath> paths = new List<AudioPath>();
            var seenDistances = new HashSet<int>();

            foreach (var ray in primaryReflections)
            {
                if (!ray.IsValid) continue;

                int roundedDistance = Mathf.RoundToInt(ray.DistanceToImage * 1000f); // ~0.001er Auflösung
                if (seenDistances.Contains(roundedDistance)) continue;

                RaycastHit hit;

                Vector3 toTarget = target.transform.position - (Vector3)ray.ImagePosition;
                Vector3 toSource = source.transform.position - (Vector3)ray.ImagePosition;

                int ignoreLayer = 6;
                int layerMask = ~(1 << ignoreLayer); // Invertiert: Alle Layer außer 6
                Physics.Raycast(ray.ImagePosition, toTarget.normalized, out hit, toTarget.magnitude, layerMask);
                if (hit.collider == null)
                {
                    seenDistances.Add(roundedDistance);
                    paths.Add(ray);
                }
            }

            return paths;
        }


        public List<AudioPath> GetSecondaryReflections(NativeArray<RaycastHit> sourroundingHitsSource,
            NativeArray<RaycastHit> sourroundingHitsTarget, float absorption)
        {
            int maxLength = sourroundingHitsSource.Length * sourroundingHitsTarget.Length;
            NativeList<SecondaryHit> secondaryHits = new NativeList<SecondaryHit>(maxLength, Allocator.TempJob);

            GetPossibleSecondaryHits secJob = new GetPossibleSecondaryHits()
            {
                Source = source.transform.position,
                Target = target.transform.position,
                PossibleHits = secondaryHits.AsParallelWriter(),
                InitHitSource = sourroundingHitsSource,
                InitHitTarget = sourroundingHitsTarget
            };
            JobHandle secFindHandle = secJob.Schedule(sourroundingHitsSource.Length, 16);
            secFindHandle.Complete();

            NativeArray<RaycastCommand> toSource = new NativeArray<RaycastCommand>(secondaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastCommand> toTarget = new NativeArray<RaycastCommand>(secondaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastCommand> imageToImage =
                new NativeArray<RaycastCommand>(secondaryHits.Length, Allocator.TempJob);

            FillSecondaryRaycastCommandsParallel secFill = new FillSecondaryRaycastCommandsParallel()
            {
                Source = source.transform.position,
                Target = target.transform.position,
                SecHits = secondaryHits,

                ToSource = toSource,
                ToTarget = toTarget,
                ImageToImage = imageToImage
            };
            JobHandle secFillHandle = secFill.Schedule(secondaryHits.Length, 16);

            secFillHandle.Complete();

            NativeArray<RaycastHit> sourceHits = new NativeArray<RaycastHit>(secondaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastHit> targetHits = new NativeArray<RaycastHit>(secondaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastHit> imageHits = new NativeArray<RaycastHit>(secondaryHits.Length, Allocator.TempJob);

            secFindHandle.Complete();

            JobHandle toSourceHandle = RaycastCommand.ScheduleBatch(toSource, sourceHits, 1);
            JobHandle toTargetHandle = RaycastCommand.ScheduleBatch(toTarget, targetHits, 1);
            JobHandle toImageHandle = RaycastCommand.ScheduleBatch(imageToImage, imageHits, 1);

            toSourceHandle.Complete();
            toTargetHandle.Complete();
            toImageHandle.Complete();

            NativeArray<AudioPath> secPaths = new NativeArray<AudioPath>(secondaryHits.Length, Allocator.TempJob);

            CheckSecondaryRays checkSecJob = new CheckSecondaryRays()
            {
                AudioRays = secPaths,
                Source = source.transform.position,
                Target = target.transform.position,
                SecHits = secondaryHits,
                ToTargetHit = targetHits,
                ToSourceHit = sourceHits,
                ImageToImageHit = imageHits,
                Absorption = absorption
            };
            JobHandle checkHandel = checkSecJob.Schedule(secondaryHits.Length, 16);
            checkHandel.Complete();


            List<AudioPath> reflections = SaveSecondaryReflections(secPaths);

            sourceHits.Dispose();
            targetHits.Dispose();
            imageHits.Dispose();

            toSource.Dispose();
            toTarget.Dispose();
            imageToImage.Dispose();

            secondaryHits.Dispose();
            secPaths.Dispose();

            return reflections;
        }

        public List<AudioPath> GetPrimaryReflections(NativeArray<RaycastHit> surroundingHitsSource, float absorption)
        {
            if (_primaryReflections.IsCreated) _primaryReflections.Dispose();
            _primaryReflections = new NativeArray<AudioPath>(surroundingHitsSource.Length, Allocator.Persistent);

            NativeArray<RaycastCommand> commands =
                new NativeArray<RaycastCommand>(surroundingHitsSource.Length, Allocator.TempJob);


            FillPrimaryRaycastCommandsParallel filljob = new FillPrimaryRaycastCommandsParallel()
            {
                Target = target.transform.position,
                Origin = source.transform.position,
                InitHit = surroundingHitsSource,
                RaycastCommands = commands
            };
            JobHandle jobHandle = filljob.Schedule(surroundingHitsSource.Length, 8);
            NativeArray<RaycastHit> primaryHit =
                new NativeArray<RaycastHit>(filljob.RaycastCommands.Length, Allocator.TempJob);

            jobHandle.Complete();
            jobHandle = RaycastCommand.ScheduleBatch(filljob.RaycastCommands, primaryHit, 1);
            jobHandle.Complete();

            CheckPrimaryRays checkJob = new CheckPrimaryRays()
            {
                PrimaryHit = primaryHit,
                Target = target.transform.position,
                Origin = source.transform.position,
                AudioRays = _primaryReflections,
                InitHit = surroundingHitsSource
            };
            jobHandle = checkJob.Schedule(surroundingHitsSource.Length, 8);
            jobHandle.Complete();
            List<AudioPath> reflections = SavePrimaryReflections(_primaryReflections);

            reflections.ForEach(ray => ray.Energy = absorption);
            _primaryReflections.Dispose();
            commands.Dispose();
            primaryHit.Dispose();
            return reflections;
        }


        [BurstCompile]
        private struct CheckSecondaryRays : IJobParallelFor
        {
            public NativeArray<AudioPath> AudioRays;

            [ReadOnly] public NativeList<SecondaryHit> SecHits;
            [ReadOnly] public NativeArray<RaycastHit> ToSourceHit;
            [ReadOnly] public NativeArray<RaycastHit> ToTargetHit;
            [ReadOnly] public NativeArray<RaycastHit> ImageToImageHit;
            [ReadOnly] public Vector3 Source;
            [ReadOnly] public Vector3 Target;
            [ReadOnly] public float Absorption;


            public void Execute(int index)
            {
                if (ToSourceHit[index].distance < 0.0001f || ToTargetHit[index].distance < 0.0001f ||
                    ImageToImageHit[index].distance < 0.0001)
                {
                    AudioPath falsePath = new AudioPath
                    {
                        IsValid = false,
                    };

                    AudioRays[index] = falsePath;
                    return;
                }

                if (ToSourceHit[index].normal != SecHits[index].SourcePlaneNormal) return;
                if ((ToSourceHit[index].point - SecHits[index].SourcePlanePosition).magnitude > 0.01f) return;
                if (ToTargetHit[index].normal != SecHits[index].TargetPlaneNormal) return;
                if ((ToTargetHit[index].point - SecHits[index].TargetPlanePosition).magnitude > 0.01f) return;
                if (ImageToImageHit[index].normal != SecHits[index].SourcePlaneNormal) return;
                if ((ImageToImageHit[index].point - SecHits[index].SourcePlanePosition).magnitude > 0.01f) return;

                float distanceToSource = math.distance(Source, ToSourceHit[index].point);
                float distanceToTarget = math.distance(Target, ToTargetHit[index].point);
                float distanceImageToImage = math.distance(ToSourceHit[index].point, ToTargetHit[index].point);

                AudioPath path = new AudioPath
                {
                    Energy = (Absorption * Absorption),
                    DistanceToImage = distanceToSource + distanceImageToImage + distanceToTarget,
                    ImagePosition = ToTargetHit[index].point + ToTargetHit[index].normal * 0.001f,
                    IsValid = true,
                };

                path.Positions.Add(ToSourceHit[index].point);
                path.Positions.Add(ToTargetHit[index].point);
                AudioRays[index] = path;
            }
        }

        [BurstCompile]
        private struct CheckPrimaryRays : IJobParallelFor
        {
            public NativeArray<AudioPath> AudioRays;
            [ReadOnly] public NativeArray<RaycastHit> InitHit;
            [ReadOnly] public NativeArray<RaycastHit> PrimaryHit;
            [ReadOnly] public Vector3 Origin;
            [ReadOnly] public Vector3 Target;

            public void Execute(int index)
            {
                if (PrimaryHit[index].distance < 0.01f) return;
                if (PrimaryHit[index].normal != InitHit[index].normal) return;

                float distanceToSource = math.distance((float3)InitHit[index].point, Origin);
                float distanceToTarget = math.distance((float3)InitHit[index].point, Target);

                AudioPath path = new AudioPath
                {
                    Energy = 0.8f,
                    DistanceToImage = distanceToSource + distanceToTarget,
                    ImagePosition = PrimaryHit[index].point + PrimaryHit[index].normal * 0.001f,
                    IsValid = true,
                };
                path.Positions.Add(PrimaryHit[index].point);
                AudioRays[index] = path;
            }
        }

        [BurstCompile]
        private struct GetPossibleSecondaryHits : IJobParallelFor
        {
            public NativeList<SecondaryHit>.ParallelWriter PossibleHits;

            [ReadOnly] public Vector3 Source;
            [ReadOnly] public Vector3 Target;

            [ReadOnly] public NativeArray<RaycastHit> InitHitSource;
            [ReadOnly] public NativeArray<RaycastHit> InitHitTarget;


            public void Execute(int index)
            {
                Vector3 pTarget = Target;
                Vector3 toPointTarget = pTarget - InitHitTarget[index].point;
                float distTarget = math.dot(toPointTarget, InitHitTarget[index].normal);
                Vector3 mirrorTarget = pTarget - 2 * distTarget * InitHitTarget[index].normal;
                Vector3 flippedTarget = mirrorTarget;

                for (int i = 0; i < InitHitSource.Length; i++)
                {
                    if (InitHitTarget[index].normal == InitHitSource[i].normal) continue;

                    Vector3 pSource = Source;
                    Vector3 toPointsSource = pSource - InitHitSource[i].point;
                    float distSource = math.dot(toPointsSource, InitHitSource[i].normal);
                    Vector3 mirrorSource = pSource - 2 * distSource * InitHitSource[i].normal;
                    Vector3 flippedSource = mirrorSource;

                    Vector3 rayDirection = flippedTarget - flippedSource;

                    float dotSourcePlane = Vector3.Dot(rayDirection, InitHitSource[i].normal);
                    float dotTargetPlane = Vector3.Dot(rayDirection, InitHitTarget[index].normal);

                    if (Mathf.Abs(dotSourcePlane) < 0.001f) continue;
                    if (Mathf.Abs(dotTargetPlane) < 0.001f) continue;

                    float tSource = Vector3.Dot(InitHitSource[i].normal, InitHitSource[i].point - flippedSource) /
                                    dotSourcePlane;
                    float tTarget = Vector3.Dot(InitHitTarget[index].normal, InitHitTarget[index].point - flippedSource) /
                                    dotTargetPlane;

                    Vector3 intersectionPointSource =
                        flippedSource + rayDirection * tSource + InitHitSource[index].normal * 0.001f;
                    Vector3 intersectionPointTarget =
                        flippedSource + rayDirection * tTarget - InitHitTarget[index].normal * 0.001f;

                    SecondaryHit hit = new SecondaryHit()
                    {
                        SourcePlanePosition = intersectionPointSource,
                        SourcePlaneNormal = InitHitSource[i].normal,
                        TargetPlanePosition = intersectionPointTarget,
                        TargetPlaneNormal = InitHitTarget[index].normal,
                    };

                    PossibleHits.AddNoResize(hit);
                }
            }
        }

        [BurstCompile]
        private struct FillSecondaryRaycastCommandsParallel : IJobParallelFor
        {
            public NativeArray<RaycastCommand> ToSource;
            public NativeArray<RaycastCommand> ToTarget;
            public NativeArray<RaycastCommand> ImageToImage;


            [ReadOnly] public Vector3 Source;
            [ReadOnly] public Vector3 Target;
            [ReadOnly] public NativeList<SecondaryHit> SecHits;


            public void Execute(int index)
            {
                ToSource[index] = new RaycastCommand(Source, SecHits[index].SourcePlanePosition - Source,
                    QueryParameters.Default);
                ToTarget[index] = new RaycastCommand(Target, SecHits[index].TargetPlanePosition - Target,
                    QueryParameters.Default);
                ImageToImage[index] = new RaycastCommand(SecHits[index].TargetPlanePosition,
                    SecHits[index].SourcePlanePosition - SecHits[index].TargetPlanePosition, QueryParameters.Default);
            }
        }

        [BurstCompile]
        private struct FillPrimaryRaycastCommandsParallel : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RaycastHit> InitHit;
            public NativeArray<RaycastCommand> RaycastCommands;

            [ReadOnly] public Vector3 Origin;
            [ReadOnly] public Vector3 Target;


            public void Execute(int index)
            {
                Vector3 P = Target;
                Vector3 toPoint = P - InitHit[index].point;
                float distance = math.dot(toPoint, InitHit[index].normal);
                Vector3 mirrored = P - 2 * distance * InitHit[index].normal;
                Vector3 flippedTarget = mirrored;

                RaycastCommands[index] =
                    new RaycastCommand(Origin, Vector3.Normalize(flippedTarget - Origin), QueryParameters.Default);
            }
        }

        public struct SecondaryHit
        {
            public Vector3 SourcePlanePosition;
            public Vector3 SourcePlaneNormal;
            public Vector3 TargetPlanePosition;
            public Vector3 TargetPlaneNormal;
        }
    }
}