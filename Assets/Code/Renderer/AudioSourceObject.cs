using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Code.Renderer
{
    public class AudioSourceObject : MonoBehaviour
    {
        public bool openFile = false;

        [HideInInspector] public float[] audioTrack;
        [HideInInspector] public AudioSource audioSource;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (openFile)
            {
                openFile = false;
                LoadAudioTrackFromSource();
            }
        }

        public void LoadAudioTrackFromSource()
        {
            string path = EditorUtility.OpenFilePanel("Wähle eine Datei", "", "wav");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log("Ausgewählte Datei: " + path);

                // float[] Samples aus WAV-Datei lesen
                audioTrack = WaveFileImporter.ReadWavFile(path);

                // Wichtige Parameter auslesen:
                // Du brauchst diese Infos aus deiner WAV-Lesefunktion!
                // Falls du sie dort noch nicht zurückgibst, erweitere sie wie unten beschrieben.
                int channels = 2;
                int sampleRate = 48000;

                // AudioClip erzeugen
                AudioClip clip = AudioClip.Create("ImportedClip", audioTrack.Length / channels, channels, sampleRate,
                    false);

                // Samples in den Clip schreiben
                clip.SetData(audioTrack, 0);

                // Clip zuweisen
                audioSource.clip = clip;
                Debug.Log($"AudioClip geladen: {clip.samples} Samples, {clip.channels} Kanäle, {clip.frequency} Hz");

                // Optional: direkt abspielen
                audioSource.Play();
            }
        }
    }
}