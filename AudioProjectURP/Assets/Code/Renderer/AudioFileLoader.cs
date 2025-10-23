using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Code.Renderer
{
    [ExecuteAlways]
    public class AudioFileLoader 
    {
        public AudioClip Clip;
        public float[] Samples;
        public int Channels;
        [HideInInspector]
        public int Frequency;

        public void LoadClip()
        {
            if (Clip == null)
            {
                Debug.LogWarning("AudioClipToBuffer: kein Clip zugewiesen");
                Samples = null;
                return;
            }

            Channels  = Clip.channels;
            Frequency = Clip.frequency;
            int totalSamples = Clip.samples * Channels;
            Samples = new float[totalSamples];
            Clip.GetData(Samples, 0);
            Debug.Log($"AudioClipToBuffer: '{Clip.name}' geladen, {totalSamples} Samples, {Channels} Kanäle, {Frequency} Hz");
        }
    }
}