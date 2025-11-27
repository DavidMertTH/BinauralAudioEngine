using System;
using System.Collections.Generic;
using System.Linq;
using Code.Renderer;
using Code.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Analysis
{
    public class AnalysisRays : MonoBehaviour
    {
        public BinauralAudioProcessor AudioProcessor;
        public GameObject target;
        public GameObject rayPrefab;
        [HideInInspector] public List<LineRenderer> lines;

        private void Start()
        {
            lines = new List<LineRenderer>();

            for (int i = 0; i < 50; i++)
            {
                GameObject go = Instantiate(rayPrefab);
                lines.Add(go.GetComponent<LineRenderer>());
                lines[i].alignment = LineAlignment.View;
                lines[i].transform.parent = transform;
            }
        }

        private void Update()
        {
            List<AudioPath> audioPaths = AudioProcessor.GetAllSelectedPaths();
            if (audioPaths == null || audioPaths.Count == 0) return;
            int RayCount = 0;
            int lineCount = 0;
            while (true)
            {
                
                
                LineRenderer currentLine = lines[lineCount];
                AudioPath currentPath = audioPaths[RayCount];
                RayCount++;
                lineCount++;

                if (lineCount >= lines.Count) break;
            
                if (!currentPath.IsValid || currentPath.Positions.Length > 2)
                {
                    RayCount++;
                    continue;
                }
                if (RayCount >= audioPaths.Count)
                {
                    lines[lineCount].positionCount = 0;
                    continue;
                };
                lines[lineCount].positionCount = 2 + currentPath.Positions.Length;
                Vector3[] positions = new Vector3[lines[lineCount].positionCount];
                positions[0] = AudioProcessor.transform.position;
                positions[^1] = target.transform.position;
                for (int j = 0; j < currentPath.Positions.Length; j++)
                {
                    positions[1 + j] = currentPath.Positions[j];
                }
                
                lines[lineCount].startColor = new Color(1, 1, 1, currentPath.Energy);
                lines[lineCount].endColor = new Color(1, 1, 1, currentPath.Energy);

                lines[lineCount].SetPositions(positions);
                lineCount++;
            }
        }
    }
}