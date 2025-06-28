#include "Bridge.h"
#include <juce_dsp/juce_dsp.h>
#include <vector>
#include <memory>
#include <cstring>

using namespace juce::dsp;

static std::unique_ptr<Reverb>    gReverb;
static ProcessSpec               gSpec;
static std::vector<float>        gTempBuffer;   // planar: [chan0...chanN][block]
static int                        gNumChannels = 0;
static std::vector<float*>       gChannelData;  // Zeiger auf jeden Kanal

extern "C" {

JUCEUNITYDLL_API void initReverb(double sampleRate,
                                 int blockSize,
                                 int numChannels)
{
    if (sampleRate<=0 || blockSize==0 || numChannels<=0)
        return;

    gNumChannels = numChannels;
    // Spec konfigurieren
    gSpec.sampleRate       = sampleRate;
    gSpec.maximumBlockSize = static_cast<int>(blockSize);
    gSpec.numChannels      = static_cast<int>(numChannels);

    // Reverb anlegen + prepare
    gReverb.reset(new Reverb());
    gReverb->prepare(gSpec);

    // Default-Params
    Reverb::Parameters p;
    p.roomSize   = 0.6f;
    p.damping    = 0.5f;
    p.wetLevel   = 0.4f;
    p.dryLevel   = 0.6f;
    p.width      = 1.0f;
    p.freezeMode = 0.0f;
    gReverb->setParameters(p);

    // Puffer planar allokieren: numChannels * blockSize
    gTempBuffer.resize(blockSize * numChannels);

    // Channel-Pointer setzen
    gChannelData.resize(numChannels);
    for (int ch = 0; ch < numChannels; ++ch)
        gChannelData[ch] = gTempBuffer.data() + ch * blockSize;
}

JUCEUNITYDLL_API void processReverb(
    const float* input,
    float*       output)
{
    if (!gReverb || gNumChannels <= 0 || gTempBuffer.empty())
        return;

    // blockSize = gTempBuffer.size() / gNumChannels
    auto blockSize = gTempBuffer.size() / gNumChannels;

    // 1) Deinterleave: interleaved input → planar gTempBuffer
    for (std::size_t i = 0; i < blockSize; ++i)
        for (int ch = 0; ch < gNumChannels; ++ch)
            gChannelData[ch][i] = input[i * gNumChannels + ch];

    // 2) AudioBlock mit mehreren Kanälen
    AudioBlock<float> block(
        gChannelData.data(),
        static_cast<size_t>(gNumChannels),
        blockSize);

    ProcessContextReplacing<float> ctx(block);

    // 3) Reverb-Prozess
    gReverb->process(ctx);

    // 4) Reinterleave: planar → interleaved output
    for (std::size_t i = 0; i < blockSize; ++i)
        for (int ch = 0; ch < gNumChannels; ++ch)
            output[i * gNumChannels + ch] = 0.0;//gChannelData[ch][i];
}

JUCEUNITYDLL_API void shutdownReverb()
{
    gReverb.reset();
    gTempBuffer.clear();
    gChannelData.clear();
    gNumChannels = 0;
}

} // extern "C"
