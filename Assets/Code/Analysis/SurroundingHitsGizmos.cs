using Code.Core;
using UnityEngine;

namespace Code.Analysis
{
    public class SurroundingHitsGizmos : MonoBehaviour
    {
        private void Update()
        {
            for (var i = 0; i < BinauralAudioEngine.Instance.SurroundingHits.Length; i++)
            {
                var hit = BinauralAudioEngine.Instance.SurroundingHits[i];
                var isCoplanar = BinauralAudioEngine.Instance.IsCoplanar[i];
                if (hit.collider == null) continue;
                var color = isCoplanar ? Color.green : Color.red;
                Debug.DrawLine(Camera.main.transform.position, hit.point, color);
            }
        }
    }
}