// AudioProcessorDLL.h

#pragma once

class AudioProcessor
{
public:
    AudioProcessor() = default;

    // Verarbeitet das übergebene Float-Array in-place
    static void process(float* data, int numSamples)
    {
        if (data == nullptr || numSamples <= 0)
            return;

        for (int i = 0; i < numSamples; ++i)
            data[i] *= 2.0f; // Beispiel: Lautstärke verdoppeln
    }
};