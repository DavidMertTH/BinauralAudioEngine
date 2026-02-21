using System;
using System.Linq;
using Code.Renderer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SidebarUi : MonoBehaviour
{
    public Button loadFileButton;
    public TextMeshProUGUI loadedFileText;
    public AudioSourceObject activeSource;
    public Slider volumeSlider;
    public Slider timeSlider;
    private bool _suppressCallback = false;

    private BinauralAudioFilter _activeFilter;

    public
        void Start()
    {
        // Button.was += LoadNewFile;
    }

    void Update()
    {
        if (activeSource == null) return;
        _activeFilter = activeSource.GetComponent<BinauralAudioFilter>();
        _activeFilter.Volume = volumeSlider.value;
        UpdateSliderFromCode(_activeFilter.PlaybackPosition01);
    }

    public void UpdateInfos()
    {
        string[] tokens = activeSource.path.Split(new[] { "/" }, StringSplitOptions.None);
        loadedFileText.text =tokens.ToList().Last();
    }

    public void LoadNewFile()
    {
        if (activeSource == null) return;
        loadedFileText.text = activeSource.LoadAudioTrackFromSource();
        UpdateInfos();
    }
    public void StopAndStart()
    {
        if (activeSource == null) return;
        activeSource.isRunning = !activeSource.isRunning;
    }

    void UpdateSliderFromCode(float value)
    {
        _suppressCallback = true;
        timeSlider.value = value;
        _suppressCallback = false;
    }

    public void OnSliderValueChanged(float value)
    {
        if (_suppressCallback)
            return;

        _activeFilter.PlaybackPosition01 = value;
    }
}