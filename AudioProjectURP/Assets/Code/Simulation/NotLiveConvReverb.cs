using System.Numerics;
using Code.Renderer;
using MathNet.Numerics.IntegralTransforms;
using UnityEngine;

namespace Code.Simulation
{
    public class NotLiveConvReverb : MonoBehaviour
    {
        public AudioSource source;
        public AudioFileLoader AudioFileLoader;
        public AudioClip audioClip;
        public AudioClip iR;
    
        public float[] dry;
        public float[] wet;
        public float[] iRData;

        public bool convert;
        public bool changeDryWet;

        [Range(0f, 1f)] [SerializeField] public float dryWet;
    
        private AudioClip _activeClip;
        // Update is called once per frame
        private void Start()
        {
            LoadData();
        }
    
        void Update()
        {
            if (convert)
            {
                convert = false;
                Convolution();
            
                _activeClip = AudioClip.Create("ConvResult", dry.Length, 2, audioClip.frequency, false);
                _activeClip.SetData(dry, 0);
                source.clip = _activeClip;
                source.Play();
            }

            if (changeDryWet)
            {
                changeDryWet = false;
                AdjustAudioFile();
            }
        }

        private void AdjustAudioFile()
        {
            float[] result = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++)
            {
                result[i] = dry[i] * (1-dryWet)+ wet[i] * (dryWet);
            }
            _activeClip.SetData(result, 0);
            source.clip = _activeClip;
            source.Play();
        }
        private void LoadData()
        {
            AudioFileLoader.Clip = audioClip;
            AudioFileLoader.LoadClip();
            dry = AudioFileLoader.Samples;
            
            AudioFileLoader.Clip = iR;
            AudioFileLoader.LoadClip();
            iRData = AudioFileLoader.Samples;
        
            int potenz = GetMaxZweierPotenz(dry.Length);
            Debug.Log("Max Potenz: "+potenz);

            dry = FillToBufferSize((int)Mathf.Pow(2f,potenz), dry);
            iRData = FillToBufferSize((int)Mathf.Pow(2f,potenz), iRData);
            wet = new float[dry.Length];
        }

        private int GetMaxZweierPotenz(int size)
        {
            return (int)(Mathf.Log(size, 2) + 1);
        }
        private float[] FillToBufferSize(int size, float[] data)
        {
            if (data == null || data.Length > size)
            {
                Debug.Log("FillToBufferSize faild");
                return data;
            }
            float[] padBuffer = new float[size];
            for (int i = 0; i < data.Length; i++)
            {
                padBuffer[i] = data[i];
            }
            return padBuffer;
        }
        private void Convolution()
        {
            Complex[] complexAudioData = new Complex[dry.Length];
            for (int i = 0; i < complexAudioData.Length; i++)
            {
                Complex tmp = new Complex(dry[i],0);
                complexAudioData[i] = tmp;
            }
            Complex[] complexIrInput = new Complex[iRData.Length];
            for (int i = 0; i < complexIrInput.Length; i++)
            {
                Complex tmp = new Complex(iRData[i],0);
                complexIrInput[i] = tmp;
            }
            Fourier.Forward(complexAudioData, FourierOptions.Matlab);
            Fourier.Forward(complexIrInput, FourierOptions.Matlab);
        
            for (int i = 0; i < complexIrInput.Length; i++)
            {
                complexAudioData[i] *= complexIrInput[i];
            }
        
            Fourier.Inverse(complexAudioData, FourierOptions.Matlab);
        
            for (int i = 0; i < complexAudioData.Length; i++)
                complexAudioData[i] /= complexAudioData.Length;
        
            for (int i = 0; i < complexAudioData.Length; i++)
                complexAudioData[i] *=  complexAudioData.Length;
        
            float[] resultingSamples = new float[complexAudioData.Length];
        
            for (int i = 0; i < resultingSamples.Length; i++)
            {
                resultingSamples[i] = (float)complexAudioData[i].Real;
            }
            wet = resultingSamples;
        }
    }
}
