using UnityEngine;

namespace Code.EditorControlls
{
    public class CameraController : MonoBehaviour
    {
        private Camera _camera;
        private Vector3 _latestWorldPosition;
        void Start()
        {
            _camera = Camera.main;
        }

        void Update()
        {
            if (_camera == null) return;
            ListenForInputs();
        }

        private void ListenForInputs()
        {
            HandlePanning();
            HandleZooming();
            HandleReset();

        }

        private void HandleReset()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                _camera.transform.position =new Vector3(0,10,0);
            }

        }
        private void HandleZooming()
        {
            _camera.orthographicSize -= Input.mouseScrollDelta.y * 0.1f;
        }
        private void HandlePanning()
        {
            if (Input.GetKeyDown(KeyCode.Mouse2))
            {
                _latestWorldPosition = Helper.GetMouseWorldPosition(_camera);
            }

            if (Input.GetKey(KeyCode.Mouse2) && _latestWorldPosition != Vector3.zero)
            {
                Vector3 currentMouseWorldPos = Helper.GetMouseWorldPosition(_camera);
                Vector3 delta = _latestWorldPosition - currentMouseWorldPos;
                _camera.transform.position += delta;
                _latestWorldPosition = Helper.GetMouseWorldPosition(_camera);
            }

            if (Input.GetKeyUp(KeyCode.Mouse2))
            {
                _latestWorldPosition = Vector3.zero;
            }
        }
    }
}