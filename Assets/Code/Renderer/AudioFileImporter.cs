// NuGet: Install-Package NAudio
using System;
using System.IO;
using NAudio.Wave;

/// <summary>
/// Zuverlässiger Audio-Importer für WAV und MP3.
/// Gibt float[]-Samples (interleaved, normalisiert auf [-1, 1]) zurück.
/// </summary>
public static class AudioFileImporter
{
    public static int LastSampleRate   { get; private set; }
    public static int LastNumChannels  { get; private set; }
    public static int LastBitsPerSample { get; private set; }
    public static long LastTotalSamples { get; private set; } // Samples pro Kanal

    /// <summary>
    /// Liest WAV oder MP3 und gibt alle Samples interleaved als float[] zurück.
    /// </summary>
    public static float[] ReadAudioFile(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".wav"  => ReadWav(filePath),
            ".mp3"  => ReadMp3(filePath),
            ".aiff" or ".aif" => ReadWithNAudio<AiffFileReader>(filePath),
            _       => throw new NotSupportedException($"Dateiformat '{ext}' wird nicht unterstützt.")
        };
    }

    // ── WAV ──────────────────────────────────────────────────────────────────

    private static float[] ReadWav(string filePath)
    {
        using var reader = new WaveFileReader(filePath);
        return ExtractSamples(reader);
    }

    // ── MP3 ──────────────────────────────────────────────────────────────────

    private static float[] ReadMp3(string filePath)
    {
        // Mp3FileReader → WaveFormatConversionStream → IEEE Float
        using var mp3Reader = new Mp3FileReader(filePath);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
        return ExtractSamples(pcmStream);
    }

    // ── AIFF (Bonus) ─────────────────────────────────────────────────────────

    private static float[] ReadWithNAudio<T>(string filePath) where T : WaveStream
    {
        using var reader = (WaveStream)Activator.CreateInstance(typeof(T), filePath)!;
        return ExtractSamples(reader);
    }

    // ── Kern-Extraktion ───────────────────────────────────────────────────────

    /// <summary>
    /// Konvertiert beliebigen WaveStream zu float[] [-1, 1], interleaved.
    /// Unterstützt 8/16/24/32-bit PCM und 32-bit IEEE Float.
    /// </summary>
    private static float[] ExtractSamples(WaveStream stream)
    {
        var fmt = stream.WaveFormat;

        LastSampleRate    = fmt.SampleRate;
        LastNumChannels   = fmt.Channels;
        LastBitsPerSample = fmt.BitsPerSample;

        // Alles auf IEEE Float 32-bit konvertieren – NAudio macht das sauber
        var floatFormat = WaveFormat.CreateIeeeFloatWaveFormat(fmt.SampleRate, fmt.Channels);

        float[] result;
        using var convStream = new WaveFormatConversionStream(floatFormat, stream);
        {
            // Puffergröße: 4 Bytes pro float-Sample
            int bytesPerSample = 4;
            long totalBytes = convStream.Length;
            int totalFloats = (int)(totalBytes / bytesPerSample);

            result = new float[totalFloats];
            byte[] buffer = new byte[Math.Min(81920, (int)totalBytes)]; // 80 KB Chunks

            int floatIndex = 0;
            int bytesRead;
            while ((bytesRead = convStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                int floatsInChunk = bytesRead / 4;
                Buffer.BlockCopy(buffer, 0, result, floatIndex * 4, floatsInChunk * 4);
                floatIndex += floatsInChunk;
            }

            // Falls Puffer zu groß war, Array kürzen
            if (floatIndex < result.Length)
                Array.Resize(ref result, floatIndex);
        }

        LastTotalSamples = result.Length / LastNumChannels;
        return result;
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    /// <summary>
    /// Gibt nur den linken (oder mono) Kanal zurück.
    /// </summary>
    public static float[] GetMonoChannel(float[] interleaved, int channelIndex = 0)
    {
        if (LastNumChannels == 1) return interleaved;

        int channels = LastNumChannels;
        float[] mono = new float[interleaved.Length / channels];
        for (int i = 0; i < mono.Length; i++)
            mono[i] = interleaved[i * channels + channelIndex];
        return mono;
    }

    /// <summary>
    /// Gibt Metadaten als String aus (für Debug/Logging).
    /// </summary>
    public static string GetInfoString() =>
        $"SampleRate={LastSampleRate} Hz | Channels={LastNumChannels} | " +
        $"Bits={LastBitsPerSample} | Samples/ch={LastTotalSamples} | " +
        $"Duration={(double)LastTotalSamples / LastSampleRate:F2}s";
}