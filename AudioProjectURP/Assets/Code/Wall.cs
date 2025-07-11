using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code
{
    public class Wall : MonoBehaviour
    {
        public float startingValue;

        private void Start()
        {
            startingValue = Random.Range(startingValue, 4);
            MoveParts.Instance.walls.Add(this);
        }
    }
}