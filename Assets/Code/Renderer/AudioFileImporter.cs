using System;
using System.IO;

namespace Code.Renderer
{
    public static class AudioFileImporter
    {
        public static int LastSampleRate    { get; private set; }
        public static int LastNumChannels   { get; private set; }
        public static int LastBitsPerSample { get; private set; }

        public static float[] ReadWavFile(string filePath)
        {
            using var reader = new BinaryReader(File.OpenRead(filePath));

            // RIFF Header
            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF") throw new FormatException("Keine gültige WAV-Datei (kein RIFF).");
            reader.ReadUInt32(); // Dateigröße
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE") throw new FormatException("Keine gültige WAV-Datei (kein WAVE).");

            // Chunks durchsuchen
            ushort audioFormat    = 0;
            ushort numChannels    = 0;
            uint   sampleRate     = 0;
            ushort bitsPerSample  = 0;
            bool   isFloat        = false;
            long   dataPos        = -1;
            uint   dataSize       = 0;
            bool   haveFmt        = false;

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunkId   = new string(reader.ReadChars(4));
                uint   chunkSize = reader.ReadUInt32();
                long   chunkEnd  = reader.BaseStream.Position + chunkSize;

                if (chunkId == "fmt ")
                {
                    haveFmt      = true;
                    audioFormat  = reader.ReadUInt16();
                    numChannels  = reader.ReadUInt16();
                    sampleRate   = reader.ReadUInt32();
                    reader.ReadUInt32(); // byteRate
                    reader.ReadUInt16(); // blockAlign
                    bitsPerSample = reader.ReadUInt16();
                    isFloat = (audioFormat == 0x0003); // IEEE Float
                }
                else if (chunkId == "data")
                {
                    dataPos  = reader.BaseStream.Position;
                    dataSize = chunkSize;
                }

                // Zum nächsten Chunk springen (inkl. Padding bei ungerader Größe)
                reader.BaseStream.Position = chunkEnd;
                if ((chunkSize & 1) == 1 && reader.BaseStream.Position < reader.BaseStream.Length)
                    reader.BaseStream.Position++;

                if (haveFmt && dataPos >= 0) break;
            }

            if (!haveFmt)   throw new FormatException("Kein 'fmt '-Chunk gefunden.");
            if (dataPos < 0) throw new FormatException("Kein 'data'-Chunk gefunden.");

            LastSampleRate    = (int)sampleRate;
            LastNumChannels   = numChannels;
            LastBitsPerSample = bitsPerSample;

            // Samples lesen
            reader.BaseStream.Position = dataPos;
            int bytesPerSample = bitsPerSample / 8;
            int totalSamples   = (int)(dataSize / bytesPerSample);
            float[] samples    = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                switch (bitsPerSample)
                {
                    case 8:
                        samples[i] = (reader.ReadByte() - 128) / 128f;
                        break;
                    case 16:
                        samples[i] = Math.Max(-1f, reader.ReadInt16() / 32768f);
                        break;
                    case 24:
                        int b0 = reader.ReadByte(), b1 = reader.ReadByte(), b2 = reader.ReadByte();
                        int s24 = b0 | (b1 << 8) | (b2 << 16);
                        if ((s24 & 0x800000) != 0) s24 |= unchecked((int)0xFF000000);
                        samples[i] = Math.Max(-1f, s24 / 8388608f);
                        break;
                    case 32:
                        samples[i] = isFloat
                            ? reader.ReadSingle()
                            : Math.Max(-1f, reader.ReadInt32() / 2147483648f);
                        break;
                    default:
                        throw new NotSupportedException($"{bitsPerSample} Bits werden nicht unterstützt.");
                }
            }

            return samples;
        }
    }
}