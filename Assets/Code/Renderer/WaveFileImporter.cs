using NAudio.Wave;

public static class WaveFileImporter
{
    public static float[] ReadWavFile(string filePath)
    {  
        using var reader = new WaveFileReader(filePath);
        var sampleProvider = reader.ToSampleProvider();
        var samples = new float[reader.Length / 4];
        sampleProvider.Read(samples, 0, samples.Length);
        return samples;
    }
}
