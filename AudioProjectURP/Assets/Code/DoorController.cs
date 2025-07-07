using UnityEngine;

public class DoorController : MonoBehaviour
{
    public enum DoorType { Left, Right }
    public DoorType doorType = DoorType.Left;

    public float openAngle = 90f;
    public float openSpeed = 1.5f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isPlayerNearby = false;

    void Start()
    {
        closedRotation = transform.rotation;

        float angle = doorType == DoorType.Left ? -openAngle : openAngle;
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0f, angle, 0f));
    }

    void Update()
    {
        Quaternion targetRot = isPlayerNearby ? openRotation : closedRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * openSpeed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
        }
    }
}
