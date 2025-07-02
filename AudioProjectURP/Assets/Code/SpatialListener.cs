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
        [Range(0, 20)] [SerializeField] public int bounces;

        private SpatialListener _target;

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
            float t1 = Time.realtimeSinceStartup;
            _surroundingHitsSource = AudioEnvironment.Instance.GetSurfacesAroundPosition(source.transform.position);
            _surroundingHitsTarget = AudioEnvironment.Instance.GetSurfacesAroundPosition(source.transform.position);
            float t2 = Time.realtimeSinceStartup;

            binauralAudioProcessor.DirectHit = GetDirectRay(source.transform.position, _target.transform.position);
            float t3 = Time.realtimeSinceStartup;

            if (imageSource != null)
            {
                binauralAudioProcessor.PrimaryReflections = imageSource.GetPrimaryReflections(_surroundingHitsSource);
                binauralAudioProcessor.SecundaryReflections =
                    imageSource.GetSecundaryReflections(_surroundingHitsSource, _surroundingHitsTarget);
            }

            float t4 = Time.realtimeSinceStartup;

            if (raycastAudio != null)
            {
                binauralAudioProcessor.HigherOrderReflections = raycastAudio.GetHighOrderRays(
                    _target.transform.position, bounces,
                    AudioEnvironment.Instance.GetRaycastsAroundPosition(source.transform.position));
            }

            float t5 = Time.realtimeSinceStartup;

            _surroundingHitsSource.Dispose();
            _surroundingHitsTarget.Dispose();

            print("t1: "+ (t2-t1)*1000 + "   t2: "+(t3-t2)*1000+"   t3: "+ (t4-t3)*1000 + "   t4: "+(t5-t4)*1000);
        }

        private void OnDestroy()
        {
            _surroundingHitsSource.Dispose();
            _surroundingHitsTarget.Dispose();
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