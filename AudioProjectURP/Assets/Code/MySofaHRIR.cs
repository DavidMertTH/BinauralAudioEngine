using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Runtime.InteropServices;


namespace Code
{
    public class MySofaHRIR
    {

        public MYSOFA_HRIR hrtfData;

        // Beispielstruktur zum Speichern der HRIRs
        public Dictionary<(float, float), (float[], float[])> hrtfMap;
        public float radius;

        List<(float, float)> sortedList;


        public MySofaHRIR(IntPtr hrtfData)
        {
            this.hrtfData = MarshalHRIR(hrtfData);

            hrtfMap = createHRIRDictionary(this.hrtfData);

            sortedList = hrtfMap.Keys.OrderBy(key => key.Item1)
                                                          .ThenBy(key => key.Item2)
                                                          .ToList();

            (float[] leftEarResponse, float[] rightEarResponse) = hrtfMap[(0,-30)];
        }

        private Dictionary<(float, float), (float[], float[])> createHRIRDictionary(MYSOFA_HRIR hrtfData)
        {
            Dictionary<(float, float), (float[], float[])> soundso = new Dictionary<(float, float), (float[], float[])>();

            float[] completeHRIRArray = GetArrayFromIntPtr(hrtfData.DataIR.values, hrtfData.DataIR.elements);
            float[] sourcePos = GetArrayFromIntPtr(hrtfData.SourcePosition.values, hrtfData.SourcePosition.elements);

            uint hrtfLength = hrtfData.N; // Länge der einzelnen Impulsantwort
            radius = sourcePos[2];

            for (int i = 0; i < hrtfData.M; i++)
            {
                float[] extractedHRIRLeft = new float[hrtfLength];
                float[] extractedHRIRRight = new float[hrtfLength];

                Array.Copy(completeHRIRArray, i * 256 * 2, extractedHRIRLeft, 0, hrtfLength);
                Array.Copy(completeHRIRArray, (i * 256 * 2) + 256, extractedHRIRRight, 0, hrtfLength);

                soundso.Add((sourcePos[(i * 3)], sourcePos[(i * 3) + 1]), (extractedHRIRLeft, extractedHRIRRight));

                (float[] leftEarResponse, float[] rightEarResponse) = soundso[(sourcePos[(i * 3)], sourcePos[(i * 3) + 1])];

            }

            return soundso;
        }


        // Suche und Interpolation
        public (float[], float[]) FindBestHRIR(float targetAzimuth, float targetElevation)
        {
            (float, float) closestBoth = FindClosestKey(sortedList, (targetAzimuth, targetElevation));

            if (!hrtfMap.ContainsKey((closestBoth)))
            {
                throw new InvalidOperationException("Kein gültiger HRIR-Schlüssel gefunden.");
            }

            (float[] leftEarResponse, float[] rightEarResponse) = hrtfMap[(closestBoth)];

            return (leftEarResponse, rightEarResponse);
        }

        static (float, float) FindClosestKey(List<(float, float)> sortedList, (float, float) target)
        {
            // Binäre Suche nach dem nächstgelegenen x-Wert
            int left = 0;
            int right = sortedList.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (sortedList[mid].Item1 == target.Item1)
                {
                    left = mid;
                    break;
                }
                else if (sortedList[mid].Item1 < target.Item1)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            // Bestimme den nächsten Key-Pair
            (float, float) closestKey = sortedList[Math.Max(0, Math.Min(left, sortedList.Count - 1))];

            // Überprüfe Nachbarn um die präziseste Entfernung zu holen
            for (int i = Math.Max(0, left - 1); i <= Math.Min(sortedList.Count - 1, left + 1); ++i)
            {
                if (Distance(sortedList[i], target) < Distance(closestKey, target))
                {
                    closestKey = sortedList[i];
                }
            }

            return closestKey;
        }

        static double Distance((float, float) point1, (float, float) point2)
        {
            return Math.Sqrt(Math.Pow(point1.Item1 - point2.Item1, 2) + Math.Pow(point1.Item2 - point2.Item2, 2));
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

        private static MYSOFA_HRIR MarshalHRIR(IntPtr hrtfPtr)
        {
            return Marshal.PtrToStructure<MYSOFA_HRIR>(hrtfPtr);
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

    }
}