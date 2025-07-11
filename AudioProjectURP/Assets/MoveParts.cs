using System;
using System.Collections.Generic;
using Code;
using UnityEngine;
using UnityEngine.Serialization;

public class MoveParts : MonoBehaviour
{
    [FormerlySerializedAs("wall")] public List<Wall> walls;
    public static MoveParts Instance;
    public float frequency;
    public float amplitude;
    public float offset;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    void Update()
    {
        float time = Time.realtimeSinceStartup;
        foreach (Wall wall in walls)
        {
            var newPos = wall.transform.position;
            newPos.y = offset + amplitude*(Mathf.Sin(frequency*time + wall.startingValue));
            wall.transform.position = newPos;
        }
    }
}
