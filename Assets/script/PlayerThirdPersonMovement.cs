using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerThirdPersonMovement : MonoBehaviour
{
    public Transform ExitPosition;

    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Animator anim; // Ссылка на аниматор
    private Vector3 velocity;
    private bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>(); // Получаем компонент при запуске
    }

    void OnEnable()
    {
        if (ExitPosition != null)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;

            transform.position = ExitPosition.position;
            transform.rotation = Quaternion.identity;

            controller.enabled = true;
        }
        else
        {
            Debug.LogWarning("ExitPosition не назначена!");
        }
    }

    void Update()
    {
        MovePlayer();
    }

    void MovePlayer()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(x, 0f, z);
        move = Vector3.ClampMagnitude(move, 1f);

        // Проверяем, движется ли персонаж
        bool isMoving = move.magnitude > 0.01f;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Передаем состояние в Animator
        if (anim != null)
        {
            anim.SetBool("isWalk", isMoving);
        }

        controller.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}