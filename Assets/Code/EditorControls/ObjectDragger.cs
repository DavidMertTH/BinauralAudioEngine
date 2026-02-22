using System;
using Code.Core;
using Code.Renderer;
using UnityEngine;

namespace Code.EditorControlls
{
    public class ObjectDragger : MonoBehaviour
    {
        public static ObjectDragger Instance;
        private GameObject _currentlyDragging;
        public GameObject _currentlyRotating;
        public SidebarUi ui;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (_currentlyDragging != null)
            {
                _currentlyDragging.transform.position = GetClickedPosition();
                _currentlyDragging.transform.position += Vector3.up;
            }

            if (_currentlyRotating != null)
            {
                Vector3 lookAtPos = new Vector3(GetClickedPosition().x, 1, GetClickedPosition().z);
                _currentlyRotating.transform.LookAt(lookAtPos);
            }

            if (Input.GetMouseButtonUp(0) && _currentlyDragging != null)
            {
                if (_currentlyDragging.GetComponent<AudioSourceObject>() != null)
                {
                    _currentlyDragging.GetComponent<AudioSourceObject>().reloadIr = true;
                }

                if (_currentlyDragging.GetComponent<BinauralAudioFilter>() != null)
                    BinauralAudioEngine.Instance.UpdateAllImpulseResponses();
                _currentlyDragging = null;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _currentlyRotating = null;
            }
        }

        public void ListenForInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                SetDragging(GetObjectUnderMouse());
            }

            if (Input.GetMouseButtonDown(1))
            {
                GameObject hitObject = GetObjectUnderMouse();
                if (hitObject == null) return;
                if (hitObject.GetComponent<AudioSourceObject>() != null) Destroy(hitObject);
                if (hitObject.GetComponent<AudioListener>() != null) _currentlyRotating = hitObject;
            }
        }

        public void SetDragging(GameObject objectDragging)
        {
            _currentlyDragging = objectDragging;
            if (objectDragging == null) return;
            if (objectDragging.GetComponent<AudioSourceObject>() != null)
                ui.SetNewActiveSource(objectDragging.GetComponent<AudioSourceObject>());
        }

        public GameObject GetObjectUnderMouse()
        {
            Ray ray = new Ray(GetClickedPosition() + Vector3.up * 100, Vector3.down);
            RaycastHit hit;
            LayerMask mask = LayerMask.GetMask("TargetLayer", "Source");
            if (!Physics.Raycast(ray, out hit, 200, mask)) return null;
            return hit.collider.gameObject;
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