using System;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Complex32 = MathNet.Numerics.Complex32;

namespace Code
{
    public class ConvolutionReverbShap
    {
        /// <summary>
        /// IR im Frequenzbereich (Complex32-Array der Länge FFTSize).
        /// </summary>
        public Complex32[] IrFreqLeft { get; private set; }

        public Complex32[] IrFreqRight { get; private set; }

        public int FFTSize { get; }
        public int BlockSize { get; }

        // Puffer für den Überhang (Overlap-Add)
        private readonly float[] _overlap;

        // Hann-Fenster für sanfte Blockübergänge
        private readonly float[] _window;

        /// <summary>
        /// fftSize muss eine Potenz von 2 sein und >= blockSize + IR.Length - 1.
        /// </summary>
        public ConvolutionReverbShap(int fftSize, int blockSize)
        {
            if (fftSize <= 0) throw new ArgumentException(nameof(fftSize));
            if (blockSize <= 0) throw new ArgumentException(nameof(blockSize));
            if ((fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("fftSize muss Potenz von 2 sein");

            FFTSize = fftSize;
            BlockSize = blockSize;

            _overlap = new float[fftSize];

            // Hann-Fenster vorbereiten
            _window = new float[blockSize];
            for (int i = 0; i < blockSize; ++i)
                _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (blockSize - 1)));
        }

        /// <summary>
        /// Neue Impulsantwort laden: normalisieren, padden, FFT.
        /// </summary>
        public void PrepareNewImpulseResponse(float[] irTime)
        {
            if (irTime == null || irTime.Length == 0)
                throw new ArgumentException("irTime darf nicht leer sein", nameof(irTime));

            float peak = 0f;
            for (int i = 0; i < irTime.Length; ++i)
                peak = MathF.Max(peak, MathF.Abs(irTime[i]));
            if (peak > 0f)
            {
                for (int i = 0; i < irTime.Length; ++i)
                    irTime[i] /= peak;
            }

            IrFreqLeft = new Complex32[FFTSize];
            for (int i = 0; i < FFTSize; ++i)
                IrFreqLeft[i] = (i < irTime.Length)
                    ? new Complex32(irTime[i], 0f)
                    : Complex32.Zero;

            Fourier.Forward(IrFreqLeft, FourierOptions.Matlab);
            Array.Clear(_overlap, 0, _overlap.Length);
        }

        public void PrepareNewImpulseResponseRight(float[] irTime)
        {
            if (irTime == null || irTime.Length == 0)
                throw new ArgumentException("irTime darf nicht leer sein", nameof(irTime));

            float peak = 0f;
            for (int i = 0; i < irTime.Length; ++i)
                peak = MathF.Max(peak, MathF.Abs(irTime[i]));
            

            IrFreqRight = new Complex32[FFTSize];
            for (int i = 0; i < FFTSize; ++i)
                IrFreqRight[i] = (i < irTime.Length)
                    ? new Complex32(irTime[i], 0f)
                    : Complex32.Zero;

            Fourier.Forward(IrFreqRight, FourierOptions.Matlab);
            Array.Clear(_overlap, 0, _overlap.Length);
        }

        /// <summary>
        /// Verarbeitet genau einen Block der Länge BlockSize.
        /// </summary>
        public float[] ProcessBlock(float[] blockIn, Complex32[] ir)
        {
            if (blockIn == null || blockIn.Length != BlockSize)
                throw new ArgumentException($"blockIn muss Länge {BlockSize} haben");

            // 1) Input mit Fensterung in Complex-Puffer kopieren
            var buffer = new Complex32[FFTSize];
            for (int i = 0; i < BlockSize; ++i)
                buffer[i] = new Complex32(blockIn[i] * _window[i], 0f);
            for (int i = BlockSize; i < FFTSize; ++i)
                buffer[i] = Complex32.Zero;

            // 2) FFT auf Input
            Fourier.Forward(buffer, FourierOptions.Matlab);

            // 3) Multiplikation im Frequenzbereich (Faltung)
            for (int i = 0; i < FFTSize; ++i)
                buffer[i] *= ir[i];

            // 4) Inverse FFT in Zeitbereich (skaliert automatisch durch Matlab-Option)
            Fourier.Inverse(buffer, FourierOptions.Matlab);

            // 5) Overlap-Add + Fenstertaper
            var output = new float[BlockSize];
            for (int i = 0; i < BlockSize; ++i)
            {
                float val = buffer[i].Real; // bereits richtig skaliert
                float y = val + _overlap[i]; // Overlap-Add
                output[i] = y * _window[i]; // Fensterskalierung
            }

            // 6) Neuen Tail in den Überhang-Puffer schreiben
            int tailLen = FFTSize - BlockSize;
            for (int i = 0; i < tailLen; ++i)
                _overlap[i] = buffer[BlockSize + i].Real;
            // 7) Rest (BlockSize..tailLen) sicherheitshalber nullen falls nötig
            for (int i = tailLen; i < BlockSize; ++i)
                _overlap[i] = 0f;

            return output;
        }
    }
}