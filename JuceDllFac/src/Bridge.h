#pragma once
#include <cstddef>

#ifdef _WIN32
  #ifdef JUCEUNITYDLL_EXPORTS
    #define JUCEUNITYDLL_API __declspec(dllexport)
  #else
    #define JUCEUNITYDLL_API __declspec(dllimport)
  #endif
#else
  #define JUCEUNITYDLL_API
#endif

extern "C" {
  /**
   * sampleRate: z.B. 48000.0
   * blockSize : Anzahl Samples pro Kanal (z.B. 1024)
   * numChannels: 1=mono, 2=stereo
   */
  JUCEUNITYDLL_API void initReverb(double sampleRate,
                                   int blockSize,
                                   int numChannels);

  /**
   * Verarbeitet einen Block:
   *  input:  float[blockSize * numChannels] interleaved
   *  output: float[blockSize * numChannels] interleaved
   */
  JUCEUNITYDLL_API void processReverb(
      const float* input,
      float*       output);

  /** Reverb wieder freigeben */
  JUCEUNITYDLL_API void shutdownReverb();
}
