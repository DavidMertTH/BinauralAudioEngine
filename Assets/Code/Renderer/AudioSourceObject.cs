using System;
using UnityEngine;

namespace Code.Renderer
{
    public class AudioSourceObject : MonoBehaviour
    {
        public float audioTrack;
        public AudioSource audioSource;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if(audioSource == null)audioSource = gameObject.AddComponent<AudioSource>();
        }

        public void LoadAudioTrackFromSource(String path)
        {
            
        }
    }
}