# README – Echo Head Audio Engine

This project implements a high-performance binaural audio engine capable of rendering realistic spatial sound in real time. The engine integrates the following techniques:

- **Image Source Method** for early reflections  
- **Ray-traced Audio** for precise modeling of complex reflections  
- **Dynamic HRTFs** for individualized headphone filtering  
- **Partitioned Overlap-Add (OLA) Convolution** for efficient real-time processing  
![WhatsApp Bild 2025-07-11 um 22 46 27_70e25fef](https://github.com/user-attachments/assets/fd46984e-f914-4ff1-a350-6eebc4067a8d)

## Install 

Clone the repository:

```bash
git clone https://github.com/DavidMertTH/BinauralAudioEngine.git
```
Then open the AudioProjectURP folder in Unity.

- In the root folder of the project, there is an executable (`.exe`) file. This file can be run as a standalone program.
- 
## Controls

| Input                   | Function                              |
|-------------------------|---------------------------------------|
| **W, A, S, D**          | Navigate through the virtual space    |
| **Mouse**               | Look around                           |
| **F1–F4**               | Switch between predefined scenarios   |
| **Numeric Keypad 1–4**  | Select different HRTF profiles        |
| **Spacebar**            | Toggle HRTF processing on/off         |
| **H**            | Toggle Hann Windowing         |
| **B**            | Bypass Audio Engine         |

## Limitations
- The current Implementation only works on Windows

- When the impulse response changes—for example, when moving through the environment or rotating the head—strong artifacts may occur. These artifacts are very likely caused, among other factors, by spectral leakage. One way to mitigate these artifacts is to apply a Hann window to each audio input block. The user can toggle this feature by pressing **H**. However, applying the window significantly alters the audio signal and attenuates high frequencies; therefore, it is disabled by default.
![WhatsApp Bild 2025-07-11 um 22 47 07_3be9a72e](https://github.com/user-attachments/assets/a6cf54aa-28c4-42a4-9c4f-91221c5f5a29)

