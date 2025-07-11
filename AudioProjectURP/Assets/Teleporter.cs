using System;
using System.Collections.Generic;
using Code;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

public class Teleporter : MonoBehaviour
{
    [SerializeField] public GameObject cam;
    [SerializeField] public GameObject audioSource;

    [SerializeField] public List<Transform> camPositions;
    [SerializeField] public List<Transform> source;
    [SerializeField] public SpatialListener listener;
    [SerializeField] public GameObject livingRoom;
    [SerializeField] public GameObject space;

    private void Awake()
    {
        livingRoom.SetActive(false);
        space.SetActive(false);
    }

    private void Update()
    {
        int jumpPos = -1;
        if (Input.GetKeyDown(KeyCode.F1))
        {
            listener.absorbtion = 0.9f;
            jumpPos = 0;
            livingRoom.SetActive(false);
            space.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            listener.absorbtion = 0.9f;
            jumpPos = 1;
            livingRoom.SetActive(false);
            space.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            listener.absorbtion = 0.6f;
            jumpPos = 2;
            livingRoom.SetActive(false);
            space.SetActive(true);
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            listener.absorbtion = 0.3f;
            jumpPos = 3;
            livingRoom.SetActive(true);
            space.SetActive(false);
        }

        if (jumpPos != -1)
        {
            JumpTo(jumpPos);
            jumpPos = -1;
        }
    }

    private void JumpTo(int position)
    {
        cam.transform.position = camPositions[position].position;
        cam.transform.rotation = camPositions[position].rotation;
        audioSource.transform.position = source[position].position;
        audioSource.transform.rotation = source[position].rotation;
    }
}