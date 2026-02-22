using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Code.Preprocessing
{
    public class AudioPreprocessor : IDisposable
    {
        public int numSamples;
        private bool _wasScheduled;
        private NativeArray<float> _multiChannelSamples;
        private NativeArray<float> _monoSamples;
        private NativeArray<Complex> _complexSamples;
        private readonly int _dspBufferSize;
        private readonly int _complexBlockSize;
        private readonly string _filePath;
        private readonly int _numFileChannels;
    
        public AudioPreprocessor(string filePath, int dspBufferSize, int complexBlockSize)
        {
            _filePath = filePath;
            _dspBufferSize = dspBufferSize;
            _complexBlockSize = complexBlockSize;
            using var waveFileReader = new WaveFileReader(filePath);
            _numFileChannels = waveFileReader.WaveFormat.Channels;
            var numSamples = (int) (waveFileReader.Length / 4);
            _multiChannelSamples = new NativeArray<float>(numSamples, Allocator.Persistent);
            var numMonoSamples = numSamples / waveFileReader.WaveFormat.Channels;
            _monoSamples = new NativeArray<float>(numMonoSamples, Allocator.Persistent);
            var numBlocks = numMonoSamples / _dspBufferSize;
            _complexSamples = new NativeArray<Complex>(numBlocks * complexBlockSize, Allocator.Persistent);
        }

        public JobHandle Schedule(out NativeArray<Complex> spectralAudio)
        {
            if (_wasScheduled)
                throw new InvalidOperationException("Cannot schedule the same preprocessor twice");
            _wasScheduled = true;
            
            var prevHandle = new ReadWaveFileJob
            {
                Samples = _multiChannelSamples,
                File = _filePath
            }.Schedule();
            
            var isMono = _numFileChannels == 1;
            if (!isMono)
            {
                prevHandle = new MonoMixDownJob
                {
                    MonoSamples = _monoSamples,
                    MultiChannelSamples = _multiChannelSamples.AsReadOnly(),
                    Channels = _numFileChannels
                }.ScheduleParallel(_monoSamples.Length, 64, prevHandle);
            }
            
            var toComplexHandle = new RealToComplexSamplesJob
            {
                ComplexSamples = _complexSamples,
                RealSamples = _monoSamples.AsReadOnly(),
                RealBlockSize = _dspBufferSize,
                ComplexBlockSize = _complexBlockSize
            }.ScheduleParallel(_complexSamples.Length, 32, prevHandle);
            
            var complexBlockCount = _complexSamples.Length / _complexBlockSize;
            var timeToFrequencyHandle = new TimeToFrequencySamplesJob
            {
                ComplexSamples = _complexSamples,
                BlockSize = _complexBlockSize
            }.ScheduleParallel(complexBlockCount, 32, toComplexHandle);
            numSamples = _multiChannelSamples.Length;
            spectralAudio = _complexSamples;
            return timeToFrequencyHandle;
        }

        // Cannot use Burst because of managed types
        private struct ReadWaveFileJob : IJob
        {
            public NativeArray<float> Samples;
            [ReadOnly] public FixedString512Bytes File;
            
            public void Execute()
            {
                // NAudio only works with managed arrays, but you can only get job results from native arrays
                using var reader = new WaveFileReader(File.ToString());
                var sampleProvider = reader.ToSampleProvider();
                var managedSamples = new float[Samples.Length];
                sampleProvider.Read(managedSamples, 0, managedSamples.Length);
                Samples.CopyFrom(managedSamples);
            }
        }

        [BurstCompile]
        private struct MonoMixDownJob : IJobFor
        {
            public NativeArray<float> MonoSamples;
            [ReadOnly] public NativeArray<float>.ReadOnly MultiChannelSamples;
            [ReadOnly] public int Channels;
            
            public void Execute(int index)
            {
                for (var channelIndex = 0; channelIndex < Channels; channelIndex++)
                {
                    MonoSamples[index] += MultiChannelSamples[index * Channels + channelIndex];
                }

                MonoSamples[index] /= Channels;
            }
        }
        
        [BurstCompile]
        private struct RealToComplexSamplesJob : IJobFor
        {
            public NativeArray<Complex> ComplexSamples;
            [ReadOnly] public NativeArray<float>.ReadOnly RealSamples;
            [ReadOnly] public int RealBlockSize;
            [ReadOnly] public int ComplexBlockSize;
            
            public void Execute(int index)
            {
                var blockIndex = index / ComplexBlockSize;
                var sampleIndex = index % ComplexBlockSize;
                if (sampleIndex >= RealBlockSize)
                    return;
                ComplexSamples[index] = new Complex(RealSamples[blockIndex * RealBlockSize + sampleIndex], 0);
            }
        }

        // Cannot use Burst because of managed types
        private struct TimeToFrequencySamplesJob : IJobFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Complex> ComplexSamples;
            [ReadOnly] public int BlockSize;

            public void Execute(int index)
            {
                // MathNet.Numerics only works with managed arrays, but you can only get job results from native arrays
                var slice = ComplexSamples.Slice(index * BlockSize, BlockSize);
                var managed = slice.ToArray();
                Fourier.Forward(managed, FourierOptions.Matlab);
                slice.CopyFrom(managed);
            }
        }

        public void Dispose()
        {
            _multiChannelSamples.Dispose();
            _monoSamples.Dispose();
            _complexSamples.Dispose();
        }
    }
}