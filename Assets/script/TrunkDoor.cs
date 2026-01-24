using UnityEngine;

public class TrunkDoor : MonoBehaviour
{
    public Transform hinge;      // TrunkHinge
    public float openAngle = -70f;
    public float openSpeed = 4f;
    public bool isOpen;

    Quaternion closedRot;
    Quaternion openRot;

    void Start()
    {
        closedRot = hinge.localRotation;
        openRot = Quaternion.Euler(openAngle, 0, 0) * closedRot;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
            isOpen = !isOpen;

        hinge.localRotation = Quaternion.Lerp(
            hinge.localRotation,
            isOpen ? openRot : closedRot,
            Time.deltaTime * openSpeed
        );
    }
}
