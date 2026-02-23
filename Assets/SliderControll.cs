using UnityEngine;
using UnityEngine.UI;

public class SliderControll : MonoBehaviour
{
    public Slider slider;
    public SidebarUi ui;

    private float value = 0f;
    private bool isDragging = false;

    void Start()
    {
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    void Update()
    {
        if (!isDragging)
        {
            value = ui.activeSource.audioFilter.PlaybackPosition01;
            if (value > 0 && value < 1)
            {
                slider.SetValueWithoutNotify(value);
            }
        }
    }

    void OnSliderChanged(float newValue)
    {
        if (newValue == 0) newValue = 0.001f;
        value = newValue;
    }

    public void OnDragBegin()
    {
        isDragging = true;
    }

    public void OnDragEnd()
    {
        isDragging = false;
    }
}