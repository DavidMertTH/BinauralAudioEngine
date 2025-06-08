using UnityEngine;
using System;
using System.IO;

namespace Code
{

    public static class WaveFileImporter
    {
        public static float[] LoadWav(string filePath, out int sampleRate, out int channels)
        {
            byte[] wav = File.ReadAllBytes(filePath);

            // "RIFF"
            if (System.Text.Encoding.ASCII.GetString(wav, 0, 4) != "RIFF")
                throw new Exception("Invalid WAV file: missing RIFF header");

            // "WAVE"
            if (System.Text.Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
                throw new Exception("Invalid WAV file: missing WAVE header");

            int fmtIndex = Array.IndexOf(wav, (byte)'f', 12);
            while (fmtIndex < wav.Length - 4 &&
                   System.Text.Encoding.ASCII.GetString(wav, fmtIndex, 4) != "fmt ")
            {
                fmtIndex++;
            }

            int audioFormat = BitConverter.ToInt16(wav, fmtIndex + 8);
            channels = BitConverter.ToInt16(wav, fmtIndex + 10);
            sampleRate = BitConverter.ToInt32(wav, fmtIndex + 12);
            int bitsPerSample = BitConverter.ToInt16(wav, fmtIndex + 22);

            if (audioFormat != 1 && audioFormat != 3)
                throw new Exception("Unsupported WAV format: only PCM (1) or float (3) supported");

            int dataIndex = Array.IndexOf(wav, (byte)'d', fmtIndex);
            while (dataIndex < wav.Length - 4 &&
                   System.Text.Encoding.ASCII.GetString(wav, dataIndex, 4) != "data")
            {
                dataIndex++;
            }

            int dataSize = BitConverter.ToInt32(wav, dataIndex + 4);
            int dataStart = dataIndex + 8;

            int samples = dataSize / (bitsPerSample / 8);
            float[] result = new float[samples];

            if (audioFormat == 1) // PCM int
            {
                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < samples; i++)
                    {
                        short sample = BitConverter.ToInt16(wav, dataStart + i * 2);
                        result[i] = sample / 32768f;
                    }
                }
                else
                {
                    throw new Exception("Only 16-bit PCM supported");
                }
            }
            else if (audioFormat == 3) // Float32
            {
                for (int i = 0; i < samples; i++)
                {
                    result[i] = BitConverter.ToSingle(wav, dataStart + i * 4);
                }
            }

            return result;
        }
    }
}

