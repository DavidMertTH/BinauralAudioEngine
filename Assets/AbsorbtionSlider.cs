using Code.Core;
using UnityEngine;

public class AbsorbtionSlider : MonoBehaviour
{
    public void OnValueChange(float value)
    {
        if( BinauralAudioEngine.Instance == null)return;
        BinauralAudioEngine.Instance.Settings.BounceAttenuation = value;
        BinauralAudioEngine.Instance.UpdateAllImpulseResponses();
    }
}
