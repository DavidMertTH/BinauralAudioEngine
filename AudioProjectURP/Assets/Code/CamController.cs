using UnityEngine;

public class CamController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float lookSensitivity = 2f;
    public float maxLookX = 90f;
    public float minLookX = -90f;

    private float rotX = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        LookAround();
        Move();
    }

    void LookAround()
    {
        if(!Input.GetMouseButton(0)) return;
        
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        rotX -= mouseY;
        rotX = Mathf.Clamp(rotX, minLookX, maxLookX);

        transform.localEulerAngles = new Vector3(rotX, transform.localEulerAngles.y + mouseX, 0f);
    }

    void Move()
    {
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        move.y = 0f; // bleibe auf der Ebene

        transform.position += move * moveSpeed * Time.deltaTime;
    }
}