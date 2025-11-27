using System.Collections.Generic;
using Code.Renderer;
using Unity.Collections;
using UnityEngine;

namespace Code.Simulation
{
    public class SpatialListener : MonoBehaviour
    {
        public ImageSourceOld imageSourceOld;
        public RaycastAudio raycastAudio;
        public BinauralAudioProcessor binauralAudioProcessor;
        public AudioSource source;

        public bool renderDirectRay;
        public bool renderPrimaryRay;
        public bool renderSecondaryRay;
        public bool renderHigherOrderRays;

        [Range(0, 20)] [SerializeField] public int bounces;

        [Range(0, 1)] [SerializeField] public float absorption;

        private SpatialListener _target;

        private NativeArray<RaycastHit> _surroundingHitsSource;
        private NativeArray<RaycastHit> _surroundingHitsTarget;

        private AudioPath _directPath;
        private List<AudioPath> _primaryPaths;
        private List<AudioPath> _secondaryPaths;
        private List<AudioPath> _higherOrderPaths;

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

            _directPath = GetDirectPath(source.transform.position, _target.transform.position);
            _primaryPaths = imageSourceOld.GetPrimaryReflections(_surroundingHitsSource, absorption);
            _secondaryPaths =
                imageSourceOld.GetSecondaryReflections(_surroundingHitsSource, _surroundingHitsTarget, absorption);
            _higherOrderPaths = raycastAudio.GetHighOrderRays(
                _target.transform.position, bounces,
                AudioEnvironment.Instance.GetRaycastsAroundPosition(source.transform.position), absorption);

            binauralAudioProcessor.DirectPath = _directPath;
            binauralAudioProcessor.PrimaryReflections = _primaryPaths;
            binauralAudioProcessor.SecondaryReflections = _secondaryPaths;
            binauralAudioProcessor.HigherOrderReflections = _higherOrderPaths;

            _surroundingHitsSource.Dispose();
            _surroundingHitsTarget.Dispose();
        }



        private void OnDrawGizmos()
        {
            if (_primaryPaths == null) return;
            Color color = new Color(0.5f, (10 - _directPath.DistanceToImage) / 10, 0.5f, 1f);
            Gizmos.color = color;

            if (renderDirectRay)
            {
                if (_directPath.IsValid)
                {
                    Gizmos.DrawRay(_directPath.ImagePosition,
                        (_target.transform.position - (Vector3)_directPath.ImagePosition));
                }
            }

            if (renderPrimaryRay)
            {
                foreach (AudioPath path in _primaryPaths)
                {
                    if (path.Positions.Length == 0) continue;
                    if (!path.IsValid) return;

                    color = new Color(0.5f, (10 - path.DistanceToImage) / 10, 0.5f, 1f);
                    Gizmos.color = color;

                    Gizmos.DrawRay(path.Positions[0], (_target.transform.position - (Vector3)path.ImagePosition));
                    Gizmos.DrawRay(path.Positions[0], (source.transform.position - (Vector3)path.ImagePosition));
                }
            }

            if (renderSecondaryRay)
            {
                foreach (AudioPath path in _secondaryPaths)
                {
                    if (path.Positions.Length == 0) continue;
                    if (!path.IsValid) return;

                    color = Color.blue;
                    Gizmos.color = color;

                    Gizmos.DrawRay(path.Positions[0], source.transform.position - (Vector3)path.Positions[0]);
                    Gizmos.DrawRay(path.Positions[0], (Vector3)path.Positions[1] - (Vector3)path.Positions[0]);
                    Gizmos.DrawRay(path.Positions[1], _target.transform.position - (Vector3)path.Positions[1]);
                }
            }

            if (renderHigherOrderRays)
            {
                foreach (AudioPath path in _higherOrderPaths)
                {
                    if (path.Positions.Length == 0) continue;
                    if (!path.IsValid) continue;
                    color = new Color(0, 0, 0, path.Energy/2);
                    Gizmos.color = color;

                    Gizmos.DrawRay(path.Positions[0], source.transform.position - (Vector3)path.Positions[0]);
                    for (int i = 0; i < path.Positions.Length - 1; i++)
                    {
                        Gizmos.DrawRay(path.Positions[i], (Vector3)path.Positions[i + 1] - (Vector3)path.Positions[i]);
                    }

                    Gizmos.DrawRay((Vector3)path.Positions[^1], _target.transform.position - (Vector3)path.Positions[^1]);
                }
            }
        }

        private AudioPath GetDirectPath(Vector3 localSource, Vector3 localTarget)
        {
            RaycastHit hit;
            Vector3 direction = localTarget - localSource;
            AudioPath directHit = new AudioPath();
            directHit.Energy = 1;
            LayerMask mask = LayerMask.GetMask("Wall");
          
            if ( ! Physics.Raycast(localSource, direction, out hit, direction.magnitude, mask))
            {
                directHit.DistanceToImage = 0;
                directHit.IsValid = true;
                directHit.ImagePosition = localSource;
                directHit.DistanceToImage = Vector3.Distance(localSource, localTarget);
                Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.green);
            }
            else
            {
                directHit.IsValid = false;
                Debug.DrawRay(localSource, direction.normalized * direction.magnitude, Color.red);
            }

            return directHit;
        }
    }
}