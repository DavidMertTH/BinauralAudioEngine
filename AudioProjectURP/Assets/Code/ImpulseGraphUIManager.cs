using System;
using System.Numerics;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using UnityEngine;

namespace Code
{
    public class ImpulseGraphUIManager : MonoBehaviour
    {
        public static ImpulseGraphUIManager Instance;
        public ImpulseGraphUI irLeft;
        public ImpulseGraphUI irRight;
        public ImpulseGraphUI spectreLeft;
        public ImpulseGraphUI spectreRight;
        public ImpulseGraphUI rawAudio;

        public BinauralAudioProcessor audioProcessor;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            if (audioProcessor == null) return;
            irLeft.floatBuffer = audioProcessor.impulseResponseLeft;
            irRight.floatBuffer = audioProcessor.impulseResponseRight;
            Task.Run(() =>
                spectreLeft.floatBuffer = ComputeLogEqBands(audioProcessor.leftData, audioProcessor.sampleRate, 40));
            rawAudio.floatBuffer = audioProcessor.leftData;
        }

        public static float[] ComputeLogEqBands(
            float[] audioData,
            int sampleRate,
            int numBands,
            float fMin = 20f,
            float fMax = 20000f,
            bool applyWindow = true)
        {
            // ---- Plausibilitäts-Checks ------------------------------------
            if (audioData == null) throw new ArgumentNullException(nameof(audioData));
            if ((audioData.Length & (audioData.Length - 1)) != 0)
                throw new ArgumentException("audioData length must be a power of two (256, 512, 1024 …).");
            if (fMin < 0 || fMax <= fMin) throw new ArgumentException("Frequency range invalid.");
            if (fMax > sampleRate / 2f) fMax = sampleRate / 2f; // Clamp an Nyquist
            float[] reducedAudio = new float[256];

            for (int i = 0; i < reducedAudio.Length; i++)
            {
                reducedAudio[i] = audioData[i];
            }

            int fftSize = audioData.Length;
            int halfSize = fftSize / 2;
            double binWidth = (double)sampleRate / fftSize;

            // ---- 1) Kopieren + optionales Hann-Fenster --------------------
            Complex[] fftBuf = new Complex[fftSize];

            double[] win = applyWindow ? Hann(fftSize) : null;
            double winEps = applyWindow ? WindowPowerScale(win) : fftSize; // Energieskala

            for (int i = 0; i < fftSize; i++)
            {
                double s = audioData[i];
                if (applyWindow) s *= win[i];
                fftBuf[i] = new Complex(s, 0.0);
            }

            // ---- 2) FFT ---------------------------------------------------
            Fourier.Forward(fftBuf, FourierOptions.Matlab);

            // ---- 3) Log-Bandgrenzen vorbereiten ---------------------------
            double logMin = Math.Log10(fMin);
            double logMax = Math.Log10(fMax);

            double[] bandEdge = new double[numBands + 1];
            for (int i = 0; i <= numBands; i++)
            {
                double logF = logMin + (logMax - logMin) * i / numBands;
                bandEdge[i] = Math.Pow(10.0, logF);
            }

            // ---- 4) Pegel berechnen (|X|² → 10 log10) ----------------------
            const double EPS = 1e-20;
            double norm = 2.0 / winEps; // Faktor 2, da wir nur die positive Hälfte betrachten

            float[] eq = new float[numBands];

            for (int band = 0; band < numBands; band++)
            {
                int binStart = Math.Max(1, (int)(bandEdge[band] / binWidth)); // Bin 0 (DC) skip!
                int binEnd = Math.Min(halfSize - 1,
                    (int)(bandEdge[band + 1] / binWidth));

                double powerSum = 0.0;
                int count = 0;

                for (int k = binStart; k <= binEnd; k++)
                {
                    powerSum += fftBuf[k].MagnitudeSquared();
                    count++;
                }

                double avgPower = count > 0 ? (powerSum / count) * norm : EPS;
                eq[band] = (float)(10.0 * Math.Log10(avgPower + EPS)); // dBFS
            }

            return eq;
        }

        /* ---------- Hilfsfunktionen ---------------------------------------- */

        // Eigenes Hann-Fenster (damit keine zusätzliche Lib nötig ist)
        private static double[] Hann(int N)
        {
            double[] w = new double[N];
            for (int n = 0; n < N; n++)
                w[n] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / (N - 1)));
            return w;
        }

        // Energieerhaltung: Summe(w²)/N  → für Normierung
        private static double WindowPowerScale(double[] w)
        {
            double sumSq = 0.0;
            foreach (double v in w) sumSq += v * v;
            return sumSq;
        }
    }
}