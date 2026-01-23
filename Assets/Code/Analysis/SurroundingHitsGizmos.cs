using Code.Core;
using UnityEngine;

namespace Code.Analysis
{
    public class SurroundingHitsGizmos : MonoBehaviour
    {
        private void Update()
        {
            // Show the raycasts for the listener only
            for (var i = 0; i < BinauralAudioEngine.Instance.HitsPerOrigin; i++)
            {
                var hit = BinauralAudioEngine.Instance.SurroundingHits[i];
                var isCoplanar = BinauralAudioEngine.Instance.IsCoplanar[i];
                var color = isCoplanar ? Color.grey : Color.green;
                Debug.DrawLine(Camera.main.transform.position, hit.point, color);
            }
        }
    }
}