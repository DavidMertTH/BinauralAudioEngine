using UnityEngine;

public class AudioMaterial : MonoBehaviour
{
    [Range(0, 1)] [SerializeField] public float absorbingFactor;
    [Range(0, 1)] [SerializeField] public float spreadingFaktor;
}