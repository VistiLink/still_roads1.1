using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerThirdPersonMovement : MonoBehaviour
{
    public Transform ExitPosition;

    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (ExitPosition != null)
        {
            transform.position = ExitPosition.position;
            transform.rotation = Quaternion.identity; // смотрит по Z+
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

        if (move.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        controller.Move(move * moveSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
