using System;
using Unity.Collections;
using UnityEngine;

namespace Code
{
    public class SpacialListener : MonoBehaviour
    {
        public ImageSource imageSource;
        public RaycastAudio raycastAudio;
        public BinauralAudioProcessor binauralAudioProcessor;
        public AudioSource source;
        [Range(0,20)][SerializeField] public int bounces;

        private SpacialListener _target;

        private NativeArray<RaycastHit> _surroundingHitsSource;
        private NativeArray<RaycastHit> _surroundingHitsTarget;

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
            binauralAudioProcessor.DirectHit = GetDirectRay(source.transform.position, _target.transform.position);

            _surroundingHitsSource = AudioEnvironment.instance.GetSurfacesAroundPosition(source.transform.position);
            _surroundingHitsTarget = AudioEnvironment.instance.GetSurfacesAroundPosition(source.transform.position);

            binauralAudioProcessor.DirectHit = GetDirectRay(source.transform.position, _target.transform.position);
            if (imageSource != null)
            {
                binauralAudioProcessor.PrimaryReflections = imageSource.GetPrimaryReflections(_surroundingHitsSource);
                binauralAudioProcessor.SecundaryReflections =
                    imageSource.GetSecundaryReflections(_surroundingHitsSource, _surroundingHitsTarget);
            }

            if (raycastAudio != null)
            {
                binauralAudioProcessor.HigherOrderReflections = raycastAudio.GetHighOrderRays(2,
                    _target.transform.position, bounces,
                    AudioEnvironment.instance.GetRaycastsAroundPosition(source.transform.position));
            }

            _surroundingHitsSource.Dispose();
            _surroundingHitsTarget.Dispose();
        }

        private AudioRay GetDirectRay(Vector3 localSource, Vector3 localTarget)
        {
            RaycastHit hit;
            Vector3 direction = localTarget - localSource;
            AudioRay directHit = new AudioRay();
            if (Physics.Raycast(localSource, direction, out hit, direction.magnitude))
            {
                directHit.IsValid = false;
                Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.red);
            }
            else
            {
                Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.green);
                directHit.DistanceToImage = 0;
                directHit.IsValid = true;
                directHit.ImagePosition = localSource;
            }

            return directHit;
        }
    }
}