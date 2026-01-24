using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform player;          // Player (перетащи в Inspector)
    public float mouseSensitivity = 3f;
    public float minY = -30f;
    public float maxY = 60f;
    public float rotationSmoothTime = 0.1f; // плавность поворота

    private float rotationY = 0f;
    private float rotationX = 0f;
    private Vector3 currentRotation;
    private Vector3 rotationSmoothVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        // Получаем движение мыши
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Вращение вверх / вниз
        rotationY -= mouseY;
        rotationY = Mathf.Clamp(rotationY, minY, maxY);

        // Вращение влево / вправо с плавностью
        rotationX += mouseX;
        Vector3 targetRotation = new Vector3(rotationY, rotationX, 0f);
        currentRotation = Vector3.SmoothDamp(currentRotation, targetRotation, ref rotationSmoothVelocity, rotationSmoothTime);

        // Применяем вращение к Pivot
        transform.localRotation = Quaternion.Euler(currentRotation.x, 0f, 0f);
        player.rotation = Quaternion.Euler(0f, currentRotation.y, 0f);
    }
}
