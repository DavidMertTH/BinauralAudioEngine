// Interface.cpp

#include "AudioProcessor.h"

// Instanz der Klasse (Singleton-Stil)
static AudioProcessor processor;

extern "C"
{
    // Unity ruft diese Funktion auf
    __declspec(dllexport) void processBuffer(float* buffer, int length)
    {
        processor.process(buffer, length);
    }
}
