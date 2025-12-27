using Code.Core;
using Code.Simulation;
using UnityEngine;

namespace Code.Analysis
{
    /// <summary>
    /// Traces audio paths in the Editor
    /// </summary>
    public class AudioPathGizmos : MonoBehaviour
    {
        [SerializeField] private bool _drawDirectPaths = true;
        [SerializeField] private bool _drawOneBouncePaths = true;
        [SerializeField] private bool _drawTwoBouncePaths = true;

        private readonly Color[] _colors = new[] { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan };

        private void Update()
        {
            if (!BinauralAudioEngine.Instance.IsReady) return;
            foreach (var path in BinauralAudioEngine.Instance.AudioPaths)
            {
                if (!path.IsValid || path.Positions.Length < 2)
                    continue;
                var numBounces = path.Positions.Length - 2;
                if (numBounces == 0 && !_drawDirectPaths)
                    continue;
                if (numBounces == 1 && !_drawOneBouncePaths)
                    continue;
                if (numBounces == 2 && !_drawTwoBouncePaths)
                    continue;
                var lineStartPos = path.Positions[0];
                for (var j = 1; j < path.Positions.Length; j++)
                {
                    Debug.DrawLine(lineStartPos, path.Positions[j], _colors[path.SourceIndex % _colors.Length]);
                    lineStartPos = path.Positions[j];
                }
            }
        }
    }
}