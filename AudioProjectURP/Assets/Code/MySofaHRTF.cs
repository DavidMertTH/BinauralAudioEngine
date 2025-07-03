using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Runtime.InteropServices;


namespace Code
{
    public class MySofaHRTF
    {

        private MYSOFA_HRTF hrtfData;

        // Beispielstruktur zum Speichern der HRTFs
        public Dictionary<(float, float, float), (float[], float[])> hrtfMap;

        public List<float> azimuth;
        public List<float> elevation;
        public List<float> radius;


        public MySofaHRTF(IntPtr hrtfData)
        {
            this.hrtfData = MarshalHRTF(hrtfData);

            hrtfMap = createHRTFDictionary(this.hrtfData);

            azimuth = hrtfMap.Keys.Select(key => key.Item1).Distinct().OrderBy(x => x).ToList();
            elevation = hrtfMap.Keys.Select(key => key.Item2).Distinct().OrderBy(x => x).ToList();
            radius = hrtfMap.Keys.Select(key => key.Item3).Distinct().OrderBy(x => x).ToList();
        }

        private float[] GetArrayFromIntPtr(IntPtr valuesPtr, uint numberOfElements)
        {
            float[] result = new float[numberOfElements];
            // Berechne die Größe in Bytes, da ein float 4 Bytes groß ist
            int byteCount = (int)(numberOfElements * sizeof(float));
            // Kopiere die Daten von unmanaged memory zu managed array
            Marshal.Copy(valuesPtr, result, 0, (int)numberOfElements);
            return result;
        }

        private static MYSOFA_HRTF MarshalHRTF(IntPtr hrtfPtr)
        {
            return Marshal.PtrToStructure<MYSOFA_HRTF>(hrtfPtr);
        }

        private static MYSOFA_ARRAY MarshalArray(IntPtr hrtfPtr)
        {
            return Marshal.PtrToStructure<MYSOFA_ARRAY>(hrtfPtr);
        }

        private static MYSOFA_VARIABLE MarshalVariable(IntPtr hrtfPtr)
        {
            return Marshal.PtrToStructure<MYSOFA_VARIABLE>(hrtfPtr);
        }

        private static MYSOFA_ATTRIBUTE MarshalAttribute(IntPtr hrtfPtr)
        {
            return Marshal.PtrToStructure<MYSOFA_ATTRIBUTE>(hrtfPtr);
        }

        Dictionary<(float, float, float), (float[], float[])> createHRTFDictionary(MYSOFA_HRTF hrtfData)
        {
            Dictionary<(float, float, float), (float[], float[])> soundso = new Dictionary<(float, float, float), (float[], float[])>();

            float[] completeHRTFArray = GetArrayFromIntPtr(hrtfData.DataIR.values, hrtfData.DataIR.elements);
            float[] sourcePos = GetArrayFromIntPtr(hrtfData.SourcePosition.values, hrtfData.SourcePosition.elements);

            uint hrtfLength = hrtfData.N; // Länge der einzelnen Impulsantwort
            float[] extractedHRTFLeft = new float[hrtfLength];
            float[] extractedHRTFRight = new float[hrtfLength];

            for (int i = 0; i < hrtfData.M; i++)
            {
                Array.Copy(completeHRTFArray, i * 256 * 2, extractedHRTFLeft, 0, hrtfLength);
                Array.Copy(completeHRTFArray, (i * 256 * 2) + 256, extractedHRTFRight, 0, hrtfLength);

                soundso.Add((sourcePos[(i * 3)], sourcePos[(i * 3) + 1], sourcePos[(i * 3) + 2]), (extractedHRTFLeft, extractedHRTFRight));
            }



            return soundso;
        }

        // Suche und Interpolation
        public (float[], float[]) FindBestHRTF(float targetAzimuth, float targetElevation, float targetRadius)
        {
            float closestAzimuth = FindClosest(azimuth, targetAzimuth);
            float closestElevation = FindClosest(elevation, targetElevation);
            float closestRadius = FindClosest(radius, targetRadius);

            // Nutze die ermittelten nächsten Werte als Schlüssel im Dictionary
            (float[] leftEarResponse, float[] rightEarResponse) = hrtfMap[(closestAzimuth, closestElevation, closestRadius)];

            return (leftEarResponse, rightEarResponse);
        }

        // Hilfsfunktion zum Finden des nächsten Werts
        float FindClosest(List<float> sortedCollection, float target)
        {
            int left = 0;
            int right = sortedCollection.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (sortedCollection[mid] == target)
                    return sortedCollection[mid];
                else if (sortedCollection[mid] < target)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            float closest = sortedCollection[left % sortedCollection.Count];
            if (left > 0)
            {
                float before = sortedCollection[left - 1];
                if (Mathf.Abs(before - target) < Mathf.Abs(closest - target))
                    closest = before;
            }

            return closest;
        }

    }
}