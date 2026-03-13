# Binaural Audio Engine
<img width="1913" height="1079" alt="UI" src="https://github.com/user-attachments/assets/07ed206c-d7bd-473f-b154-6e99c8cfb93d" />

This repository contains a binaural audio engine developed as part of the
VAO course at TH Köln. The system extends an existing real-time ray-tracing-based
room acoustics engine by introducing support for multiple concurrent sound sources,
an offline block-wise convolution pipeline, and an intuitive graphical user interface
for interactive scene design. The simulation combines the image source method for
low-order reflections with iterative ray tracing for higher-order reflections to compute
binaural room impulse responses. The offline architecture enables longer reverberation
tails while maintaining perceived responsiveness during the convolution process
through an asynchronous parallel convolution.
