using UnityEngine;

public class SecondaryBox : MonoBehaviour
{
    [Header("Settings")]
    public float destroyDelay = 0.2f; // задержка перед исчезновением после падения

    private bool inCar = false;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Метод должен быть public, чтобы его вызывал BoxAndTrunkSmooth
    public void SetInCar()
    {
        inCar = true;
        if (rb != null)
            rb.isKinematic = false; // включаем физику после попадания в багажник
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Если коробка в машине и коснулась земли
        if (inCar && collision.gameObject.layer == LayerMask.NameToLayer("groundbox"))
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}
