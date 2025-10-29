using System;
using UnityEngine;

namespace Code.EditorControlls
{
    public class ObjectDragger : MonoBehaviour
    {
        private GameObject _currentlyDragging;

        private void Update()
        {
            if (_currentlyDragging != null)
            {
                _currentlyDragging.transform.position = GetClickedPosition(); 
                _currentlyDragging.transform.position += Vector3.up;
            }


            if (Input.GetMouseButtonUp(0))
            {
                _currentlyDragging = null;
            }
        }

        public void ListenForInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = new Ray(GetClickedPosition() + Vector3.up * 100, Vector3.down);
                RaycastHit hit;
                LayerMask mask = LayerMask.GetMask("TargetLayer", "Source");
                if (!Physics.Raycast(ray, out hit, 200, mask)) return;
                print(hit.collider.gameObject.name);
                _currentlyDragging = hit.collider.gameObject;
            }
        }

        public bool HitsDraggableObject()
        {
            Ray ray = new Ray(GetClickedPosition() + Vector3.up * 100, Vector3.down);
            RaycastHit hit;
            LayerMask mask = LayerMask.GetMask("TargetLayer", "Source");
            return Physics.Raycast(ray, out hit, 200, mask);
        }

        private Vector3 GetClickedPosition()
        {
            Vector3 clickedPosition = Helper.GetMouseWorldPosition(Camera.main);
            clickedPosition.y = 0;

            return clickedPosition;
        }
    }
}