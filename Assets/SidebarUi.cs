using System;
using System.Collections.Generic;
using System.Linq;
using Code.Renderer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SidebarUi : MonoBehaviour
{
    public List<Image> adaptiveColorImages;
    public Button loadFileButton;
    public TextMeshProUGUI loadedFileText;
    public AudioSourceObject activeSource;
    public GraphRenderer irGraph;
    public Slider volumeSlider;
    private float _suppressCallback;
    private Color _colorToDisplay;

    public
        void Start()
    {
        // Button.was += LoadNewFile;
    }

    public void SetNewActiveSource(AudioSourceObject audioSource)
    {
        activeSource = audioSource;
        _colorToDisplay = audioSource.color;
        volumeSlider.value = activeSource.audioFilter.Volume;
    }

    void Update()
    {
        _suppressCallback -= Time.deltaTime;
        if (activeSource == null) return;
        activeSource.audioFilter.Volume = volumeSlider.value;
        adaptiveColorImages.ForEach(img => img.color = activeSource.color);
        if(activeSource.irLeft != null) irGraph.SetData(activeSource.irLeft);

    }


    public void UpdateInfos()
    {
        string[] tokens = activeSource.path.Split(new[] { "/" }, StringSplitOptions.None);
        loadedFileText.text = tokens.ToList().Last();
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
        activeSource.audioFilter.enabled = !activeSource.audioFilter.enabled;
    }

    private void ChangePlayHead(float playHeadPosition)
    {
        if (activeSource == null) return;
        activeSource.audioFilter.PlaybackPosition01 = playHeadPosition;
    }
    
}