using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Code
{
    [ExecuteAlways]
    public class AudioFileLoader : MonoBehaviour
    {
        [Header("Assign your AudioClip here")]
        public AudioClip clip;

        [Space]
        [Tooltip("Wird nach Laden mit clip.samples*clip.channels Länge gefüllt")]
        [HideInInspector]
        public float[] samples;

        [HideInInspector]
        public int channels;

        [HideInInspector]
        public int frequency;

        // Lädt den Clip in den Buffer (in Play-Mode automatisch, im Editor per Button)
        public void LoadClip()
        {
            if (clip == null)
            {
                Debug.LogWarning("AudioClipToBuffer: kein Clip zugewiesen");
                samples = null;
                return;
            }

            channels  = clip.channels;
            frequency = clip.frequency;
            int totalSamples = clip.samples * channels;
            samples = new float[totalSamples];
            clip.GetData(samples, 0);
            Debug.Log($"AudioClipToBuffer: '{clip.name}' geladen, {totalSamples} Samples, {channels} Kanäle, {frequency} Hz");
        }

        // Im Play-Mode automatisch beim Start laden
        void Awake()
        {
            if (Application.isPlaying)
                LoadClip();
        }

#if UNITY_EDITOR
        // Editor-Button unterhalb der Inspector-Felder
        [CustomEditor(typeof(AudioFileLoader))]
        public class AudioClipToBufferEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                AudioFileLoader loader = (AudioFileLoader)target;
                if (GUILayout.Button("Load Clip into Buffer"))
                {
                    loader.LoadClip();
                    // Bei Editor-Mode Änderungen speichern
                    EditorUtility.SetDirty(loader);
                }
            }
        }
#endif
    }
}