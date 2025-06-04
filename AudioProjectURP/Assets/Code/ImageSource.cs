using System;
using System.Collections.Generic;
using System.Diagnostics;
using Code;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

public class ImageSource : MonoBehaviour
{
    public Camera target;
    public AudioSource source;
    [FormerlySerializedAs("audioTest")] public BinauralAudioProcessor binauralAudioProcessor;
    public bool calculateAcoustics;
    private NativeArray<AudioRay> _primaryReflections;
    private NativeArray<AudioRay> _secundaryReflections;

    private List<AudioRay> _primaryReflectionsList;
    private List<AudioRay> _secundaryReflectionsList;


    private void Update()
    {
        binauralAudioProcessor.SecundaryReflections = _secundaryReflectionsList;
    }

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

            // Runde Richtung (z. B. auf 2 Dezimalstellen, also *100)
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
        //Debug.Log("Got " + rays.Count + "secundary ray(s)");

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

            if (!Physics.Raycast(ray.ImagePosition, toTarget.normalized, out hit, toTarget.magnitude, layerMask))
            {
                seenDistances.Add(roundedDistance);
                rays.Add(ray);
            }
            else
            {
                Debug.DrawRay(ray.ImagePosition, hit.point - (Vector3)ray.ImagePosition, Color.yellow);
                Debug.DrawRay(ray.ImagePosition, toSource, Color.yellow);
            }
        }

        return rays;
    }


    private void OnDrawGizmos()
    {
        if (_primaryReflectionsList == null) return;

        foreach (AudioRay ray in _primaryReflectionsList)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(ray.ImagePosition, 0.1f);
            Gizmos.DrawRay(ray.ImagePosition, target.transform.position - (Vector3)ray.ImagePosition);
            Gizmos.DrawRay(ray.ImagePosition, source.transform.position - (Vector3)ray.ImagePosition);
        }
    }

    public List<AudioRay> GetSecundaryReflections(NativeArray<RaycastHit> sourroundingHitsSource,
        NativeArray<RaycastHit> sourroundingHitsTarget)
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
            ImageToImageHit = imageHits
        };
        JobHandle checkHandel = checkSecJob.Schedule(secundaryHits.Length, 16);
        checkHandel.Complete();


        List<AudioRay> reflections = SaveSecundaryReflections(secRays);
        DrawSecundaryRays(secRays, secundaryHits);

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

    public void DrawSecundaryRays(NativeArray<AudioRay> secundaryRays, NativeList<SecundaryHit> secundaryHits)
    {
        for (int i = 0; i < secundaryRays.Length; i++)
        {
            if (!secundaryRays[i].IsValid) continue;
            Debug.DrawRay(source.transform.position, secundaryHits[i].SourcePlanePosition - source.transform.position,
                Color.magenta);
            Debug.DrawRay(secundaryHits[i].SourcePlanePosition,
                secundaryHits[i].TargetPlanePosition - secundaryHits[i].SourcePlanePosition, Color.magenta);
            Debug.DrawRay(target.transform.position, secundaryHits[i].TargetPlanePosition - target.transform.position,
                Color.magenta);
        }
    }

    public List<AudioRay> GetPrimaryReflections(NativeArray<RaycastHit> surroundingHits)
    {
        if (_primaryReflections.IsCreated) _primaryReflections.Dispose();
        _primaryReflections = new NativeArray<AudioRay>(surroundingHits.Length, Allocator.Persistent);

        NativeArray<RaycastCommand> commands =
            new NativeArray<RaycastCommand>(surroundingHits.Length, Allocator.TempJob);


        FillPrimaryRaycastCommandsParallel filljob = new FillPrimaryRaycastCommandsParallel()
        {
            Target = target.transform.position,
            Origin = source.transform.position,
            InitHit = surroundingHits,
            RaycastCommands = commands
        };
        JobHandle jobHandle = filljob.Schedule(surroundingHits.Length, 8);
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
            InitHit = surroundingHits
        };
        jobHandle = checkJob.Schedule(surroundingHits.Length, 8);
        jobHandle.Complete();
        List<AudioRay> reflections = SavePrimaryReflections(_primaryReflections);
        
        reflections.ForEach(ray => ray.Absorbtion = 0.8f);
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

        public void Execute(int index)
        {
            if (ToSourceHit[index].distance < 0.001f || ToTargetHit[index].distance < 0.001f ||
                ImageToImageHit[index].distance < 0.001) return;

            if (ToSourceHit[index].normal != SecHits[index].SourcePlaneNormal) return;
            if (ToSourceHit[index].point != SecHits[index].SourcePlanePosition) return;
            if (ToTargetHit[index].normal != SecHits[index].TargetPlaneNormal) return;
            if (ToTargetHit[index].point != SecHits[index].TargetPlanePosition) return;
            if (ImageToImageHit[index].normal != SecHits[index].TargetPlaneNormal) return;
            if (ImageToImageHit[index].point != SecHits[index].TargetPlanePosition) return;


            AudioRay ray = new AudioRay
            {
                Absorbtion = (0.8f*0.8f),
                DistanceToImage = ToSourceHit[index].distance + ImageToImageHit[index].distance,
                ImagePosition = ToTargetHit[index].point + ToTargetHit[index].normal * 0.001f,
                IsValid = true,
            };
            
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
            //if (PrimaryHit[index].triangleIndex != InitHit[index].triangleIndex) return;
            if (PrimaryHit[index].normal != InitHit[index].normal) return;


            AudioRay ray = new AudioRay
            {
                Absorbtion = 0.8f,
                DistanceToImage = PrimaryHit[index].distance,
                ImagePosition = PrimaryHit[index].point + PrimaryHit[index].normal * 0.001f,
                IsValid = true,
            };

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

                Vector3 intersectionPointSource = flippedSource + rayDirection * tSource;
                Vector3 intersectionPointTarget = flippedSource + rayDirection * tTarget;

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
            ImageToImage[index] = new RaycastCommand(SecHits[index].SourcePlanePosition,
                SecHits[index].TargetPlanePosition - SecHits[index].SourcePlanePosition, QueryParameters.Default);
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

            RaycastCommands[index] = new RaycastCommand(Origin, flippedTarget - Origin, QueryParameters.Default);
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