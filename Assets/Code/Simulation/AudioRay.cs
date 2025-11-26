using Unity.Collections;
using Unity.Mathematics;

namespace Code.Simulation
{
    /// <summary>
    /// Documents the path taken by an audio 'particle' so that direction and frequency absorption can be
    /// modeled in the impulse response.
    /// </summary>
    public struct AudioRay
    {
        public int Reflections;
        public float3 ImagePosition;
        public float DistanceToImage;
        public bool IsValid;
        public float Absorbtion;
        public float ScatteringDivergence;
        public FixedList512Bytes<float3> Positions;
    }
}
