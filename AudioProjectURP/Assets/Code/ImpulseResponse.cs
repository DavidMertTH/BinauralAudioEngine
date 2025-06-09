using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code
{
    public class ImpulseResponse : MonoBehaviour
    {
        public string wavFilePath;
        public bool useFromFile;
        public bool loadNow;

        [HideInInspector] public float[] audioData;
        [HideInInspector] public int sampleRate;
        [HideInInspector] public int channels;
        
        private void Update()
        {
            if (loadNow)
            {
                LoadWavFile();
            }
        }

        public void LoadWavFile()
        {
            if (!System.IO.File.Exists(wavFilePath))
            {
                Debug.LogError("Dateipfad existiert nicht: " + wavFilePath);
                return;
            }

            audioData = WaveFileImporter.LoadWav(wavFilePath, out sampleRate, out channels);
            Debug.Log($"WAV geladen: {audioData.Length} Samples, {channels} Kanäle, {sampleRate} Hz");
        }
    }
}
