using System;
using System.Collections.Generic;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    public class RaysToIr
    {
        public static (float[], float[]) CreateBrirLeftAndRight(AudioPath[] rays, int irLength,
            Transform listener, int sampleRate, int Gain)
        {
            string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa";
            int errorCode;
            IntPtr hrtfPtr = DllDemoIntegration.mysofa_load(filePath, out errorCode);
            MySofaHRIR sofaHrir = new MySofaHRIR(hrtfPtr);

            // Erst alle Rays durchgehen und maximale benötigte IR-Länge berechnen
            int requiredIrLength = 0;
            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                float distanceToSource = ray.DistanceToImage + sofaHrir.radius;
                float propagationDelaySec = distanceToSource / 343f;
                int delaySamples = (int)(sampleRate * propagationDelaySec);
                int neededLength = (int)(delaySamples + sofaHrir.hrtfData.N);

                if (neededLength > requiredIrLength)
                    requiredIrLength = neededLength;
            }


            int adaptiveIrLength = requiredIrLength > 0
                ? Mathf.Min(requiredIrLength, irLength)
                : irLength;

            Debug.Log($"[BRIR] requiredIrLength={requiredIrLength}, adaptiveIrLength={adaptiveIrLength}");

            float[] impulseResponseLeft = new float[adaptiveIrLength];
            float[] impulseResponseRight = new float[adaptiveIrLength];

            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                Vector3 vecSourceListener = listener.position -
                                            new Vector3(ray.ImagePosition.x, ray.ImagePosition.y, ray.ImagePosition.z);
                Vector3 listenerUp = listener.up;
                Vector3 listenerForward = listener.forward;

                float azimuth = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(listenerUp, listenerForward), vecSourceListener.normalized),
                    Vector3.Dot(listenerForward, vecSourceListener)) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(Vector3.Dot(vecSourceListener, listenerUp)) * Mathf.Rad2Deg;
                azimuth += 180;

                (float[] rightEarResponse, float[] leftEarResponse) = sofaHrir.FindBestHRIR(azimuth, elevation);

                if (leftEarResponse == null || rightEarResponse == null) continue;

                float distanceToSource = ray.DistanceToImage + sofaHrir.radius;
                float propagationDelaySec = distanceToSource / 343f;
                float propagationDelaySamples = sampleRate * propagationDelaySec;
                float distanceAmplitudeTwo = (ray.Energy* Gain) / (distanceToSource) ;

                for (int i = 0; i < sofaHrir.hrtfData.N; i++)
                {
                    int idx = i + (int)propagationDelaySamples;
                    if (idx >= adaptiveIrLength || propagationDelaySamples < 0) break;

                    impulseResponseLeft[idx] += leftEarResponse[i] * distanceAmplitudeTwo;
                    impulseResponseRight[idx] += rightEarResponse[i] * distanceAmplitudeTwo;
                }
            }

            return (impulseResponseRight, impulseResponseLeft);
        }
    }
}