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

    public
        void Start()
    {
        // Button.was += LoadNewFile;
    }

    void Update()
    {
        if (activeSource == null) return;
        activeSource.volume = volumeSlider.value;
        UpdateSliderFromCode(((float)activeSource.currentPlayBackHead) / activeSource.audioChunkAmount);
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
    private void ChangePlayHead(float playHeadPosition)
    {
        if (activeSource == null) return;
        activeSource.currentPlayBackHead = (int)(playHeadPosition * activeSource.audioChunkAmount);
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

        ChangePlayHead(value);
        activeSource.currentPlayBackHead = (int)(timeSlider.value * activeSource.audioChunkAmount);
    }
}