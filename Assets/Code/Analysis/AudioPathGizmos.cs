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
        [SerializeField] private bool _drawHigherOrderBouncePaths = true;

        private readonly Color[] _colors = new[] { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan };

        private void Update()
        {
            foreach (var path in BinauralAudioEngine.Instance.AudioPaths)
            {
                if (!path.IsValid || path.Positions.Length < 2)
                    continue;
                if (path.Reflections == 0 && !_drawDirectPaths)
                    continue;
                if (path.Reflections == 1 && !_drawOneBouncePaths)
                    continue;
                if (path.Reflections == 2 && !_drawTwoBouncePaths)
                    continue;
                if (path.Reflections > 2 && !_drawHigherOrderBouncePaths)
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