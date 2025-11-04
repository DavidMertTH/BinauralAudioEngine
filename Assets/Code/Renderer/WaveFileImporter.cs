using System;
using System.IO;

public static class WaveFileImporter
{
    public static int LastSampleRate { get; private set; }
    public static int LastNumChannels { get; private set; }
    public static float[] ReadWavFile(string filePath)
    {
        using var reader = new BinaryReader(File.OpenRead(filePath));

        // Beispiel (aus deiner bestehenden Funktion):
        string riff = new string(reader.ReadChars(4));
        reader.ReadUInt32();
        string wave = new string(reader.ReadChars(4));

        string fmt = new string(reader.ReadChars(4));
        int fmtSize = reader.ReadInt32();
        ushort format = reader.ReadUInt16();
        ushort numChannels = reader.ReadUInt16();
        uint sampleRate = reader.ReadUInt32();

        // Speichern für später:
        LastSampleRate = (int)sampleRate;
        LastNumChannels = numChannels;

        // ---- Chunks durchsuchen: erst fmt , dann data ----
        ushort audioFormat = 0;
        ushort bitsPerSample = 0;
        ushort blockAlign = 0;
        uint byteRate = 0;

        // Für EXTENSIBLE
        ushort validBitsPerSample = 0;
        Guid subFormat = Guid.Empty; // bestimmt PCM vs IEEE Float bei 0xFFFE

        bool haveFmt = false;
        long dataPos = -1;
        uint dataSize = 0;

        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string chunkId = new string(reader.ReadChars(4));
            uint chunkSize = reader.ReadUInt32();
            long nextChunk = reader.BaseStream.Position + chunkSize;

            if (chunkId == "fmt ")
            {
                haveFmt = true;
                audioFormat   = reader.ReadUInt16(); // WICHTIG: UInt16!
                numChannels   = reader.ReadUInt16();
                sampleRate    = reader.ReadUInt32();
                byteRate      = reader.ReadUInt32();
                blockAlign    = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();

                // Es kann Extra-Bytes geben
                if (chunkSize > 16)
                {
                    ushort cbSize = reader.ReadUInt16(); // Größe der Extra-Felder

                    if (audioFormat == 0xFFFE) // WAVE_FORMAT_EXTENSIBLE
                    {
                        // Erwartet: 22 Bytes
                        validBitsPerSample = reader.ReadUInt16();
                        uint channelMask = reader.ReadUInt32(); // ungenutzt hier
                        // SubFormat GUID lesen
                        byte[] guidBytes = reader.ReadBytes(16);
                        subFormat = new Guid(guidBytes);
                        // Rest (falls cbSize > 22) überspringen
                        int remaining = cbSize - 22;
                        if (remaining > 0) reader.ReadBytes(remaining);
                    }
                    else
                    {
                        // restliche Extra-Bytes (falls vorhanden) überspringen
                        int remaining = cbSize;
                        if (remaining > 0) reader.ReadBytes(remaining);
                    }
                }
            }
            else if (chunkId == "data")
            {
                dataPos = reader.BaseStream.Position;
                dataSize = chunkSize;
            }
            else
            {
                // Unbekannten Chunk überspringen
                reader.BaseStream.Position = nextChunk;
            }

            // Padding bei ungerader Chunkgröße
            if ((chunkSize & 1) == 1 && reader.BaseStream.Position < reader.BaseStream.Length)
                reader.ReadByte();

            // Falls wir beides schon kennen, können wir abbrechen
            if (haveFmt && dataPos >= 0)
                break;
        }

        if (!haveFmt) throw new FormatException("Kein 'fmt '-Chunk gefunden.");
        if (dataPos < 0) throw new FormatException("Kein 'data'-Chunk gefunden.");

        // ---- Daten lesen ----
        reader.BaseStream.Position = dataPos;

        // Format bestimmen (inkl. EXTENSIBLE)
        bool isPcm = false;
        bool isIeeeFloat = false;

        if (audioFormat == 0x0001) isPcm = true;                // PCM
        else if (audioFormat == 0x0003) isIeeeFloat = true;     // IEEE Float
        else if (audioFormat == 0xFFFE)                         // EXTENSIBLE
        {
            // SubFormat GUIDs:
            // PCM:   {00000001-0000-0010-8000-00AA00389B71}
            // FLOAT: {00000003-0000-0010-8000-00AA00389B71}
            var pcmGuid   = new Guid("00000001-0000-0010-8000-00AA00389B71");
            var floatGuid = new Guid("00000003-0000-0010-8000-00AA00389B71");
            if (subFormat == pcmGuid) isPcm = true;
            else if (subFormat == floatGuid) isIeeeFloat = true;
            else
                throw new NotSupportedException($"WAVE_FORMAT_EXTENSIBLE SubFormat {subFormat} wird nicht unterstützt.");
            // Bei EXTENSIBLE sind die wirklich gültigen Bits oft in validBitsPerSample
            if (validBitsPerSample != 0) bitsPerSample = validBitsPerSample;
        }
        else
        {
            throw new NotSupportedException($"Audioformat 0x{audioFormat:X4} wird nicht unterstützt.");
        }

        int bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample == 0) throw new NotSupportedException("BitsPerSample ist 0 oder ungültig.");

        // Gesamtanzahl Sample-Werte über alle Kanäle
        int totalValues = (int)(dataSize / bytesPerSample);
        float[] samples = new float[totalValues];

        // Lesen
        if (isIeeeFloat && bitsPerSample == 32)
        {
            // 32-bit float interleaved
            for (int i = 0; i < totalValues; i++)
                samples[i] = reader.ReadSingle();
        }
        else if (isPcm)
        {
            switch (bitsPerSample)
            {
                case 8:
                    for (int i = 0; i < totalValues; i++)
                    {
                        byte s = reader.ReadByte();        // unsigned
                        samples[i] = (s - 128) / 128f;      // [-1,1)
                    }
                    break;
                case 16:
                    for (int i = 0; i < totalValues; i++)
                    {
                        short s = reader.ReadInt16();       // signed LE
                        samples[i] = Math.Max(-1f, s / 32768f);
                    }
                    break;
                case 24:
                    for (int i = 0; i < totalValues; i++)
                    {
                        int b0 = reader.ReadByte();
                        int b1 = reader.ReadByte();
                        int b2 = reader.ReadByte();
                        // 24-bit little-endian -> sign-extend auf 32-bit
                        int val = (b0) | (b1 << 8) | (b2 << 16);
                        if ((val & 0x00800000) != 0) val |= unchecked((int)0xFF000000);
                        samples[i] = Math.Max(-1f, val / 8388608f); // 2^23
                    }
                    break;
                case 32:
                    // 32-bit PCM (selten) – in float normalisieren
                    for (int i = 0; i < totalValues; i++)
                    {
                        int s = reader.ReadInt32();
                        samples[i] = Math.Max(-1f, s / 2147483648f); // 2^31
                    }
                    break;
                default:
                    throw new NotSupportedException($"PCM mit {bitsPerSample} Bits wird nicht unterstützt.");
            }
        }
        else
        {
            throw new NotSupportedException("Nur PCM oder IEEE Float werden unterstützt.");
        }

        return samples;
    }
}
