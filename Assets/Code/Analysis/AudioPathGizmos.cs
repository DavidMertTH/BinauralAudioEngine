using Code.Core;
using UnityEngine;

namespace Code.Analysis
{
    /// <summary>
    /// Traces audio paths in the Editor
    /// </summary>
    public class AudioPathGizmos : MonoBehaviour
    {
        private void Update()
        {
            if (!BinauralAudioEngine.Instance.IsReady) return;
            foreach (var path in BinauralAudioEngine.Instance.AudioPaths)
            {
                if (!path.IsValid || path.Positions.Length < 2)
                    continue;
                var lineStartPos = path.Positions[0];
                for (var i = 1; i < path.Positions.Length; i++)
                {
                    Debug.DrawLine(lineStartPos, path.Positions[i], Color.grey);
                    lineStartPos = path.Positions[i];
                }
            }
        }
    }
}