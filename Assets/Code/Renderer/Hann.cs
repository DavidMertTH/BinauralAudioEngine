using System;

namespace Code.Renderer
{
    public class Hann 
    {
        public static double[] HannDouble(int N)
        {
            if (N < 1)
                return new double[0];
            if (N == 1)
                return new double[] { 1.0 };
    
            double[] w = new double[N];
            double twoPi = 2.0 * Math.PI;
            double denom = N - 1;
    
            for (int n = 0; n < N; n++)
            {
                w[n] = 0.5 * (1.0 - Math.Cos(twoPi * n / denom));
            }
    
            return w;
        }
        public static float[] HannFloat(int N)
        {
            var wDouble = HannDouble(N);
            float[] w = new float[N];
            for (int i = 0; i < N; i++)
                w[i] = (float)wDouble[i];
            return w;
        }
    
        /// <summary>
        /// Wendet in-place ein Hann-Fenster auf das gegebene Signal an.
        /// </summary>
        /// <param name="data">Signal-Array, wird direkt modifiziert</param>
        public static void ApplyHann(double[] data)
        {
            int N = data.Length;
            var w = HannDouble(N);
            for (int i = 0; i < N; i++)
                data[i] *= w[i];
        }
    
        /// <summary>
        /// Wendet in-place ein Hann-Fenster auf das gegebene float-Signal an.
        /// </summary>
        public static void ApplyHann(float[] data)
        {
            int N = data.Length;
            var w = HannFloat(N);
            for (int i = 0; i < N; i++)
                data[i] *= w[i];
        }

    }
}
