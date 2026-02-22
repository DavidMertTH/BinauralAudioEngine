using System;
using System.Collections.Generic;
using Code.EditorControlls;
using Code.Renderer;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class SourceManager : MonoBehaviour
{
    public static SourceManager Instance;
    public GameObject sourcePrefab;
    private AudioSourceObject _activeObject;
    public SidebarUi ui;
    public List<AudioSourceObject> sourceObjects;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);

        sourceObjects = new List<AudioSourceObject>();
    }

    public void Register(AudioSourceObject audioSourceObject)
    {
        sourceObjects.Add(audioSourceObject);
        SetActiveSource(audioSourceObject);
    }
    public void DeRegister(AudioSourceObject audioSourceObject)
    {
        sourceObjects.Remove(audioSourceObject);
        if (_activeObject == audioSourceObject) _activeObject = null;
        ReloadAllIrs();
    }
   
    public void AddNewAudioSource()
    {
        Debug.Log("Adding AudioSource");
        GameObject go = Instantiate(sourcePrefab);
        ObjectDragger.Instance.SetDragging(go);
        SetActiveSource(go.GetComponent<AudioSourceObject>());
    }

    public void ReloadAllIrs()
    {
        if (sourceObjects == null) return;
        sourceObjects.ForEach(source => source.reloadIr = true);
    }
    public void SetActiveSource(AudioSourceObject audioSourceObject)
    {
        _activeObject = audioSourceObject;
        ui.SetNewActiveSource(_activeObject);
    }

    public static Color NextColor(float saturation = 0.6f, float value = 0.9f)
    {
        return Color.HSVToRGB(Random.value, saturation, value);
    }
}