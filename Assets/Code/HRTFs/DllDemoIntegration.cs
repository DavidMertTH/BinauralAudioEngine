using System.Runtime.InteropServices;
using UnityEngine;
using System;
using Code.Simulation;

namespace Code
{
    public class DllDemoIntegration : MonoBehaviour
    {


        [DllImport("MyLibrary")]
        public static extern int add(int a, int b);

        [DllImport("hrtf_import")]
        public static extern int multiply(int a, int b);

        [DllImport("hrtf_import")]
        public static extern IntPtr mysofa_load(string filename, out int err);


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

        private float[] GetArrayFromIntPtr(IntPtr valuesPtr, uint numberOfElements)
        {
            float[] result = new float[numberOfElements];
            // Berechne die Größe in Bytes, da ein float 4 Bytes groß ist
            int byteCount = (int)(numberOfElements * sizeof(float));
            // Kopiere die Daten von unmanaged memory zu managed array
            Marshal.Copy(valuesPtr, result, 0, (int)numberOfElements);
            return result;
        }


        float CalculateAzimuth(Vector3 rayPos, Vector3 listenerPos)
        {
            return 0;
        }

        float CalculateElevation(Vector3 rayPos, Vector3 listenerPos)
        {
            return 0;
        }

        void ApplyHRIRFromSOFA(string sofaFilePath, AudioPath ray, float[] inputSignal)
        {
            int errorCode;
            Vector3 listenerPosition = new Vector3(0, 0, 0);

            IntPtr hrtfPtr = mysofa_load(sofaFilePath, out errorCode);

            float azimuth = CalculateAzimuth(ray.ImagePosition, listenerPosition);
            float elevation = CalculateElevation(ray.ImagePosition, listenerPosition);

            //var hrtfData = sofaData.GetHRIRDataForAngle(azimuth, elevation);

            // 4. Anwenden der HRIR durch Faltung
            //float[] leftEarSignal = Convolve(inputSignal, hrtfData.LeftEarResponse);
            //float[] rightEarSignal = Convolve(inputSignal, hrtfData.RightEarResponse);

            // 5. Hinzufügen zu den Gesamtsignalen
            //AddToBinauralMix(leftEarSignal, rightEarSignal);
        }

        /*float[] Convolve(float[] input, float[] impulseResponse)
        {
            // Implementierung der Faltung, z.B. mithilfe von FFT
            return ConvolutionLibrary.FFTConvolve(input, impulseResponse);
        }*/


        void Start()
        {
            int result = add(3, 4);
            Debug.Log("3 + 4 = " + result);

            int result2 = multiply(3, 4);
            Debug.Log("3 * 4 = " + result2);


            string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa"; // Pfad zur Datei
            int errorCode;

            // Rufe die externe Funktion auf
            IntPtr hrtfPtr = mysofa_load(filePath, out errorCode);

            if (hrtfPtr == IntPtr.Zero)
            {
                Debug.LogError("hallo, da ist was schief gelaufen");
            }
            else
            {
                Debug.Log("SOFA-Datei erfolgreich geladen!");
            }

            // Dekodiere die Struktur aus dem Pointer
            MYSOFA_HRIR hrtfData = MarshalHRIR(hrtfPtr);

            // Nutz die Daten...
            Debug.Log("Number of measurements (M): " + hrtfData.M);
            Debug.Log("Number of samples per measurements (N): " + hrtfData.N);


            float[] sourcePos = GetArrayFromIntPtr(hrtfData.SourcePosition.values, hrtfData.SourcePosition.elements);
            float[] listener = GetArrayFromIntPtr(hrtfData.EmitterPosition.values, hrtfData.EmitterPosition.elements);
            float[] impulseResponse = GetArrayFromIntPtr(hrtfData.DataIR.values, hrtfData.DataIR.elements);
            float[] delay = GetArrayFromIntPtr(hrtfData.DataDelay.values, hrtfData.DataDelay.elements);
            float[] sample = GetArrayFromIntPtr(hrtfData.DataSamplingRate.values, hrtfData.DataSamplingRate.elements);

            //MYSOFA_ARRAY pos = MarshalArray(hrtfData.ListenerPosition);
            Debug.Log("Source Position: (" + sourcePos[0] + ", " + sourcePos[1] + ", " + sourcePos[2] + ")");
            Debug.Log("Num source position elements: " + hrtfData.SourcePosition.elements);

            Debug.Log("Emitter position: (" + listener[0] + ", " + listener[1] + ", " + listener[2] + ")");
            Debug.Log("Num Emitter elements: " + hrtfData.ListenerPosition.elements);

            Debug.Log("Impulse Response: " + impulseResponse[0]);
            Debug.Log("Num Impulse Responses: " + hrtfData.DataIR.elements);

            Debug.Log("Delay: " + delay[0]);
            Debug.Log("Num Delay: " + hrtfData.DataDelay.elements);

            Debug.Log("sampling Rate: " + sample[0]);
            Debug.Log("Num Delay: " + hrtfData.DataSamplingRate.elements);
        }
    }
}