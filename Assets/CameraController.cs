using UnityEngine;

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
    }

    private void HandleZooming()
    {
        _camera.orthographicSize -= Input.mouseScrollDelta.y * 0.1f;
    }
    private void HandlePanning()
    {
        if (Input.GetKeyDown(KeyCode.Mouse2))
        {
            _latestWorldPosition = GetMouseWorldPosition();
        }

        if (Input.GetKey(KeyCode.Mouse2) && _latestWorldPosition != Vector3.zero)
        {
            Vector3 currentMouseWorldPos = GetMouseWorldPosition();
            Vector3 delta = _latestWorldPosition - currentMouseWorldPos;
            _camera.transform.position += delta;
            _latestWorldPosition = GetMouseWorldPosition();
        }

        if (Input.GetKeyUp(KeyCode.Mouse2))
        {
            _latestWorldPosition = Vector3.zero;
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        float z = -_camera.transform.position.z;
        Vector3 mouseScreenPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, z);
        return _camera.ScreenToWorldPoint(mouseScreenPos);
    }
}