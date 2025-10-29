using System;
using UnityEditor;
using UnityEngine;

namespace Code.Renderer
{
    public class AudioSourceObject : MonoBehaviour
    {
        public float[] audioTrack;
        public AudioSource audioSource;
        public bool openFile = false;
        public int sampleRate;
        public int channels;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if(audioSource == null)audioSource = gameObject.AddComponent<AudioSource>();
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
                audioTrack = WaveFileImporter.LoadWav(path, out sampleRate, out channels);
                print(sampleRate);
            }
        }
    }
}