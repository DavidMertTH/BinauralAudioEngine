using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CamController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float lookSensitivity = 2f;
    public float maxLookX = 90f;
    public float minLookX = -90f;

    public Transform cameraTransform; // Assign the Main Camera here
    public GameObject sphere;         // Assign in Inspector

    private float rotX = 0f;
    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        MoveToPosition(1); // 🔹 Start at position 1
    }

    void Update()
    {
        LookAround();
        Move();
        CheckPositionChange();
    }

    void LookAround()
    {
        if (!Input.GetMouseButton(0)) return;

        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        // Horizontal rotation on Player
        transform.Rotate(0f, mouseX, 0f);

        // Vertical rotation on Camera
        rotX -= mouseY;
        rotX = Mathf.Clamp(rotX, minLookX, maxLookX);
        cameraTransform.localEulerAngles = new Vector3(rotX, 0f, 0f);
    }

    void Move()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * moveSpeed * Time.deltaTime);
    }

    void CheckPositionChange()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) MoveToPosition(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) MoveToPosition(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) MoveToPosition(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) MoveToPosition(4);
    }

    void MoveToPosition(int positionIndex)
    {
        Vector3 camPos = Vector3.zero;
        Quaternion camRot = Quaternion.identity;
        Vector3 spherePos = Vector3.zero;

        switch (positionIndex)
        {
            case 1:
                camPos = new Vector3(-6.61999989f, 1.5f, -2.25f);
                camRot = Quaternion.Euler(0f, 90f, 0f);
                spherePos = new Vector3(-6.65999985f, 2.48000002f, 2.28999996f);
                break;
            case 2:
                camPos = new Vector3(-62.7900009f, 0.5f, -7.19000006f);
                camRot = Quaternion.Euler(0f, 90f, 0f);
                spherePos = new Vector3(-52.5340004f, 0.159999996f, 6.07600021f);
                break;
            case 3:
                camPos = new Vector3(-107.010002f, 1.5f, 1.88999999f);
                camRot = Quaternion.Euler(0f, 90f, 0f);
                spherePos = new Vector3(-97.1399994f, 1.28799999f, -0.230000004f);
                break;
            case 4:
                camPos = new Vector3(-219.509995f, 1.5f, 4.53000021f);
                camRot = Quaternion.Euler(0f, 0f, 0f);
                spherePos = new Vector3(-197.845001f, 6.0619998f, 3.70799994f);
                break;
            default:
                return;
        }

        // Move the player GameObject
        transform.position = camPos;
        transform.rotation = camRot;

        // Reset vertical camera rotation
        rotX = cameraTransform.localEulerAngles.x;

        // Move sphere
        if (sphere != null)
        {
            sphere.transform.position = spherePos;
        }
    }
}
