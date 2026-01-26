using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerThirdPersonMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        MovePlayer();
    }

    void MovePlayer()
    {
        // Проверка, стоит ли на земле
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Получаем ввод WASD
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Направление движения (локально относительно игрока)
        Vector3 move = transform.right * x + transform.forward * z;
        move.y = 0f; // игнорируем вертикаль

        controller.Move(move * moveSpeed * Time.deltaTime);

        // Гравитация
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
