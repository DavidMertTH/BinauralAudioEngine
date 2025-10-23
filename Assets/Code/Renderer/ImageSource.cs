using System;
using System.Collections.Generic;
using Code.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Renderer
{
    public class ImageSource : MonoBehaviour
    {
        public GameObject target;
        public AudioSource source;
        [FormerlySerializedAs("audioTest")] public BinauralAudioProcessor binauralAudioProcessor;
        public bool calculateAcoustics;
        private NativeArray<AudioRay> _primaryReflections;
        private NativeArray<AudioRay> _secundaryReflections;

        private List<AudioRay> _primaryReflectionsList;
        private List<AudioRay> _secundaryReflectionsList;


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

        private List<AudioRay> SaveSecundaryReflections(NativeArray<AudioRay> inputRays)
        {
            var rays = new List<AudioRay>();
            var seen = new HashSet<RayIdentifier>();

            foreach (var ray in inputRays)
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
                rays.Add(ray);
            }

            return rays;
        }

        private List<AudioRay> SavePrimaryReflections(NativeArray<AudioRay> primaryReflections)
        {
            List<AudioRay> rays = new List<AudioRay>();
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
                    rays.Add(ray);
                }
            }

            return rays;
        }


        public List<AudioRay> GetSecundaryReflections(NativeArray<RaycastHit> sourroundingHitsSource,
            NativeArray<RaycastHit> sourroundingHitsTarget, float absorbtion)
        {
            int maxLength = sourroundingHitsSource.Length * sourroundingHitsTarget.Length;
            NativeList<SecundaryHit> secundaryHits = new NativeList<SecundaryHit>(maxLength, Allocator.TempJob);

            GetPossibleSecundaryHits secJob = new GetPossibleSecundaryHits()
            {
                Source = source.transform.position,
                Target = target.transform.position,
                PossibleHits = secundaryHits.AsParallelWriter(),
                InitHitSource = sourroundingHitsSource,
                InitHitTarget = sourroundingHitsTarget
            };
            JobHandle secFindHandle = secJob.Schedule(sourroundingHitsSource.Length, 16);
            secFindHandle.Complete();

            NativeArray<RaycastCommand> toSource = new NativeArray<RaycastCommand>(secundaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastCommand> toTarget = new NativeArray<RaycastCommand>(secundaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastCommand> imageToImage =
                new NativeArray<RaycastCommand>(secundaryHits.Length, Allocator.TempJob);

            FillSecundaryRaycastCommandsParallel secFill = new FillSecundaryRaycastCommandsParallel()
            {
                Source = source.transform.position,
                Target = target.transform.position,
                SecHits = secundaryHits,

                ToSource = toSource,
                ToTarget = toTarget,
                ImageToImage = imageToImage
            };
            JobHandle secFillHandle = secFill.Schedule(secundaryHits.Length, 16);

            secFillHandle.Complete();

            NativeArray<RaycastHit> sourceHits = new NativeArray<RaycastHit>(secundaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastHit> targetHits = new NativeArray<RaycastHit>(secundaryHits.Length, Allocator.TempJob);
            NativeArray<RaycastHit> imageHits = new NativeArray<RaycastHit>(secundaryHits.Length, Allocator.TempJob);

            secFindHandle.Complete();

            JobHandle toSourceHandle = RaycastCommand.ScheduleBatch(toSource, sourceHits, 1);
            JobHandle toTargetHandle = RaycastCommand.ScheduleBatch(toTarget, targetHits, 1);
            JobHandle toImageHandle = RaycastCommand.ScheduleBatch(imageToImage, imageHits, 1);

            toSourceHandle.Complete();
            toTargetHandle.Complete();
            toImageHandle.Complete();

            NativeArray<AudioRay> secRays = new NativeArray<AudioRay>(secundaryHits.Length, Allocator.TempJob);

            CheckSecundaryRays checkSecJob = new CheckSecundaryRays()
            {
                AudioRays = secRays,
                Source = source.transform.position,
                Target = target.transform.position,
                SecHits = secundaryHits,
                ToTargetHit = targetHits,
                ToSourceHit = sourceHits,
                ImageToImageHit = imageHits,
                Absorbtion = absorbtion
            };
            JobHandle checkHandel = checkSecJob.Schedule(secundaryHits.Length, 16);
            checkHandel.Complete();


            List<AudioRay> reflections = SaveSecundaryReflections(secRays);

            sourceHits.Dispose();
            targetHits.Dispose();
            imageHits.Dispose();

            toSource.Dispose();
            toTarget.Dispose();
            imageToImage.Dispose();

            secundaryHits.Dispose();
            secRays.Dispose();

            return reflections;
        }

        public List<AudioRay> GetPrimaryReflections(NativeArray<RaycastHit> surroundingHitsSource, float absorbtion)
        {
            if (_primaryReflections.IsCreated) _primaryReflections.Dispose();
            _primaryReflections = new NativeArray<AudioRay>(surroundingHitsSource.Length, Allocator.Persistent);

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
            List<AudioRay> reflections = SavePrimaryReflections(_primaryReflections);

            reflections.ForEach(ray => ray.Absorbtion = absorbtion);
            _primaryReflections.Dispose();
            commands.Dispose();
            primaryHit.Dispose();
            return reflections;
        }


        [BurstCompile]
        private struct CheckSecundaryRays : IJobParallelFor
        {
            public NativeArray<AudioRay> AudioRays;

            [ReadOnly] public NativeList<SecundaryHit> SecHits;
            [ReadOnly] public NativeArray<RaycastHit> ToSourceHit;
            [ReadOnly] public NativeArray<RaycastHit> ToTargetHit;
            [ReadOnly] public NativeArray<RaycastHit> ImageToImageHit;
            [ReadOnly] public Vector3 Source;
            [ReadOnly] public Vector3 Target;
            [ReadOnly] public float Absorbtion;


            public void Execute(int index)
            {
                if (ToSourceHit[index].distance < 0.0001f || ToTargetHit[index].distance < 0.0001f ||
                    ImageToImageHit[index].distance < 0.0001)
                {
                    AudioRay falseRay = new AudioRay
                    {
                        IsValid = false,
                    };

                    AudioRays[index] = falseRay;
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

                AudioRay ray = new AudioRay
                {
                    Absorbtion = (Absorbtion * Absorbtion),
                    DistanceToImage = distanceToSource + distanceImageToImage + distanceToTarget,
                    ImagePosition = ToTargetHit[index].point + ToTargetHit[index].normal * 0.001f,
                    IsValid = true,
                };

                ray.Positions.Add(ToSourceHit[index].point);
                ray.Positions.Add(ToTargetHit[index].point);
                AudioRays[index] = ray;
            }
        }

        [BurstCompile]
        private struct CheckPrimaryRays : IJobParallelFor
        {
            public NativeArray<AudioRay> AudioRays;
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

                AudioRay ray = new AudioRay
                {
                    Absorbtion = 0.8f,
                    DistanceToImage = distanceToSource + distanceToTarget,
                    ImagePosition = PrimaryHit[index].point + PrimaryHit[index].normal * 0.001f,
                    IsValid = true,
                };
                ray.Positions.Add(PrimaryHit[index].point);
                AudioRays[index] = ray;
            }
        }

        [BurstCompile]
        private struct GetPossibleSecundaryHits : IJobParallelFor
        {
            public NativeList<SecundaryHit>.ParallelWriter PossibleHits;

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

                    SecundaryHit hit = new SecundaryHit()
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
        private struct FillSecundaryRaycastCommandsParallel : IJobParallelFor
        {
            public NativeArray<RaycastCommand> ToSource;
            public NativeArray<RaycastCommand> ToTarget;
            public NativeArray<RaycastCommand> ImageToImage;


            [ReadOnly] public Vector3 Source;
            [ReadOnly] public Vector3 Target;
            [ReadOnly] public NativeList<SecundaryHit> SecHits;


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

        public struct SecundaryHit
        {
            public Vector3 SourcePlanePosition;
            public Vector3 SourcePlaneNormal;
            public Vector3 TargetPlanePosition;
            public Vector3 TargetPlaneNormal;
        }
    }
}