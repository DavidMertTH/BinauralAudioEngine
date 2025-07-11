using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Code
{
    public class SpatialListener : MonoBehaviour
    {
        public ImageSource imageSource;
        public RaycastAudio raycastAudio;
        public BinauralAudioProcessor binauralAudioProcessor;
        public AudioSource source;

        public bool renderDirectRay;
        public bool renderPrimaryRay;
        public bool renderSecondaryRay;
        public bool renderHigherOrderRays;

        [Range(0, 20)] [SerializeField] public int bounces;

        [Range(0, 1)] [SerializeField] public float absorbtion;

        private SpatialListener _target;

        private NativeArray<RaycastHit> _surroundingHitsSource;
        private NativeArray<RaycastHit> _surroundingHitsTarget;

        private AudioRay _directRay;
        private List<AudioRay> _primaryRays;
        private List<AudioRay> _secondaryRays;
        private List<AudioRay> _higherOrderRays;

        private void Awake()
        {
            _target = this;
        }

        private void Update()
        {
            UpdateAudioProcessor();
        }

        private void UpdateAudioProcessor()
        {
            float t1 = Time.realtimeSinceStartup;
            _surroundingHitsSource = AudioEnvironment.Instance.GetSurfacesAroundPosition(source.transform.position);
            _surroundingHitsTarget = AudioEnvironment.Instance.GetSurfacesAroundPosition(source.transform.position);
            float t2 = Time.realtimeSinceStartup;

            _directRay = GetDirectRay(source.transform.position, _target.transform.position);
            _primaryRays = imageSource.GetPrimaryReflections(_surroundingHitsSource, absorbtion);
            _secondaryRays =
                imageSource.GetSecundaryReflections(_surroundingHitsSource, _surroundingHitsTarget, absorbtion);
            _higherOrderRays = raycastAudio.GetHighOrderRays(
                _target.transform.position, bounces,
                AudioEnvironment.Instance.GetRaycastsAroundPosition(source.transform.position), absorbtion);

            binauralAudioProcessor.DirectHit = _directRay;
            binauralAudioProcessor.PrimaryReflections = _primaryRays;
            binauralAudioProcessor.SecundaryReflections = _secondaryRays;
            binauralAudioProcessor.HigherOrderReflections = _higherOrderRays;

            _surroundingHitsSource.Dispose();
            _surroundingHitsTarget.Dispose();
        }



        private void OnDrawGizmos()
        {
            if (_primaryRays == null) return;
            Color color = new Color(0.5f, (10 - _directRay.DistanceToImage) / 10, 0.5f, 1f);
            Gizmos.color = color;

            if (renderDirectRay)
            {
                if (_directRay.IsValid)
                {
                    Gizmos.DrawRay(_directRay.ImagePosition,
                        (_target.transform.position - (Vector3)_directRay.ImagePosition));
                }
            }

            if (renderPrimaryRay)
            {
                foreach (AudioRay ray in _primaryRays)
                {
                    if (ray.Positions.Length == 0) continue;
                    if (!ray.IsValid) return;

                    color = new Color(0.5f, (10 - ray.DistanceToImage) / 10, 0.5f, 1f);
                    Gizmos.color = color;

                    Gizmos.DrawRay(ray.Positions[0], (_target.transform.position - (Vector3)ray.ImagePosition));
                    Gizmos.DrawRay(ray.Positions[0], (source.transform.position - (Vector3)ray.ImagePosition));
                }
            }

            if (renderSecondaryRay)
            {
                foreach (AudioRay ray in _secondaryRays)
                {
                    if (ray.Positions.Length == 0) continue;
                    if (!ray.IsValid) return;

                    color = Color.blue;
                    Gizmos.color = color;

                    Gizmos.DrawRay(ray.Positions[0], source.transform.position - (Vector3)ray.Positions[0]);
                    Gizmos.DrawRay(ray.Positions[0], (Vector3)ray.Positions[1] - (Vector3)ray.Positions[0]);
                    Gizmos.DrawRay(ray.Positions[1], _target.transform.position - (Vector3)ray.Positions[1]);
                }
            }

            if (renderHigherOrderRays)
            {
                foreach (AudioRay ray in _higherOrderRays)
                {
                    if (ray.Positions.Length == 0) continue;
                    if (!ray.IsValid) continue;
                    color = new Color(0, 0, 0, ray.Absorbtion/2);
                    Gizmos.color = color;

                    Gizmos.DrawRay(ray.Positions[0], source.transform.position - (Vector3)ray.Positions[0]);
                    for (int i = 0; i < ray.Positions.Length - 1; i++)
                    {
                        Gizmos.DrawRay(ray.Positions[i], (Vector3)ray.Positions[i + 1] - (Vector3)ray.Positions[i]);
                    }

                    Gizmos.DrawRay((Vector3)ray.Positions[^1], _target.transform.position - (Vector3)ray.Positions[^1]);
                }
            }
        }

        private AudioRay GetDirectRay(Vector3 localSource, Vector3 localTarget)
        {
            RaycastHit hit;
            Vector3 direction = localTarget - localSource;
            AudioRay directHit = new AudioRay();
            directHit.Absorbtion = 1;
            if (Physics.Raycast(localSource, direction, out hit, direction.magnitude))
            {
                directHit.IsValid = false;
                //Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.red);
            }
            else
            {
                //Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.green);
                directHit.DistanceToImage = 0;
                directHit.IsValid = true;
                directHit.ImagePosition = localSource;
                directHit.DistanceToImage = Vector3.Distance(localSource, localTarget);
            }

            return directHit;
        }
    }
}