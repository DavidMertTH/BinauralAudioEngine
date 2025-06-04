using Unity.Mathematics;

namespace Code
{
    public struct AudioRay
    {
        public float3 ImagePosition;
        public float DistanceToImage;
        public bool IsValid;
        public float Absorbtion;
    }
}
