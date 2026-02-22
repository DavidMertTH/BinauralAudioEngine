using System;
using System.Collections.Generic;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    public class RaysToIr
    {
        public static (float[], float[]) CreateBrirLeftAndRight(List<AudioPath> rays, int irLength,
            GameObject audioTarget, int sampleRate, int Gain)
        {
            float[] impulseResponseLeft = new float[irLength];
            float[] impulseResponseRight = new float[irLength];
            string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa";
            int errorCode;
            IntPtr hrtfPtr = DllDemoIntegration.mysofa_load(filePath, out errorCode);
            MySofaHRIR sofaHrir = new MySofaHRIR(hrtfPtr);

            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                Vector3 vecSourceListener = audioTarget.transform.position -
                                            new Vector3(ray.ImagePosition.x, ray.ImagePosition.y, ray.ImagePosition.z);
                Vector3 listenerUp = audioTarget.transform.up;
                Vector3 listenerForward = audioTarget.transform.forward;

                float azimuth = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(listenerUp, listenerForward), vecSourceListener.normalized),
                    Vector3.Dot(listenerForward, vecSourceListener)) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(Vector3.Dot(vecSourceListener, listenerUp)) * Mathf.Rad2Deg;
                azimuth += 180;
                (float[] rightEarResponse, float[] leftEarResponse) = sofaHrir.FindBestHRIR(azimuth, elevation);

                if (leftEarResponse != null && rightEarResponse != null)
                {
                    float distanceToSource = ray.DistanceToImage + (sofaHrir.radius);
                    float propagationDelaySec = distanceToSource / 343f;
                    float propagationDelaySamples = sampleRate * propagationDelaySec;
                    float distanceAmplitudeTwo = ray.Energy * (8 / distanceToSource) * Gain;

                    for (int i = 0; i < sofaHrir.hrtfData.N; i++)
                    {
                        if (i + propagationDelaySamples >= irLength - 1 || propagationDelaySamples < 0) break;

                        impulseResponseLeft[i + (int)propagationDelaySamples] +=
                            leftEarResponse[i] * distanceAmplitudeTwo;
                        impulseResponseRight[i + (int)propagationDelaySamples] +=
                            rightEarResponse[i] * distanceAmplitudeTwo;
                    }
                }
            }

            return (impulseResponseLeft, impulseResponseRight);
        }
    }
}