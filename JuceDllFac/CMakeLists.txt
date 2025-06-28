cmake_minimum_required(VERSION 3.15)

project(JuceUnityDLL VERSION 0.1.0 LANGUAGES CXX)

# JUCE Pfad anpassen!
set(JUCE_DIR "JUCE")
add_subdirectory(${JUCE_DIR})

# Export-Makro für die DLL
add_definitions(-DJUCEUNITYDLL_EXPORTS)

add_library(JuceUnityDLL SHARED
        src/AudioProcessor.cpp
        src/Bridge.cpp
        src/Bridge.h   # Header, damit du deine API auch exportieren kannst
)

target_include_directories(JuceUnityDLL PRIVATE
        ${JUCE_DIR}/modules
        src
)

target_compile_features(JuceUnityDLL PRIVATE cxx_std_17)

target_compile_definitions(JuceUnityDLL PRIVATE
        JUCE_DLL_BUILD=1
        JUCEUNITYDLL_EXPORTS
)

target_link_libraries(JuceUnityDLL
        PRIVATE
        juce::juce_core
        juce::juce_audio_basics
        juce::juce_dsp     # <--- das ganze DSP-Modul linken!)

)
# Optionale Export-Regel für Windows, damit die Symbole sichtbar sind:
if (WIN32)
    set_target_properties(JuceUnityDLL PROPERTIES
            OUTPUT_NAME "JuceUnityDLL"
    )
endif()

