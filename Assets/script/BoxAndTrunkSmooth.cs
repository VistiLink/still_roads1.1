using UnityEngine;

public class BoxAndTrunkSmooth : MonoBehaviour
{
    [Header("References")]
    public Transform holdPoint;          // точка над персонажем
    public Transform trunkPoint;         // центр багажника
    public Transform trunkDoor;          // дверь багажника
    public Transform trunkInteractPoint; // точка для проверки дистанции к багажнику
    public Rigidbody carRigidbody;       // Rigidbody машины

    [Header("Settings")]
    public float pickupRange = 2f;
    public float trunkInteractRange = 2f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode trunkKey = KeyCode.F;
    public KeyCode dropKey = KeyCode.G;      // клавиша для выброса коробки
    public float trunkOpenAngle = 70f;
    public float trunkSpeed = 2f;
    public float boxMoveSpeed = 5f;          // скорость плавного движения в багажник
    public Vector3 trunkBounds = new Vector3(1f, 0.5f, 1.5f); // половина размеров багажника
    public float trunkSoftness = 5f;         // мягкое ограничение движения в багажнике
    public float dropForce = 2f;             // сила выброса коробки
    public float pullSmoothness = 0.1f;      // плавность притягивания коробки в багажник

    [Header("Trunk Physics")]
    public TrunkBoxPhysics trunkPhysics;     // ссылка на скрипт физики багажника

    private Transform heldBox = null;        // коробка в руках
    private bool trunkOpen = false;
    private float trunkCurrentAngle = 0f;
    private bool boxMovingToTrunk = false;
    private Transform boxInTrunk = null;     // отслеживание, что коробка в багажнике
    private Vector3 boxVelocity = Vector3.zero; // для SmoothDamp

    void Update()
    {
        HandlePickup();
        HandleTrunkAnimation();
        HandleDrop();
    }

    void FixedUpdate()
    {
        HandleBoxMovement();
        ApplyTrunkSoftBounds();

        // Если коробка ушла далеко — сбрасываем статус "в багажнике"
        if (boxInTrunk != null)
        {
            float distanceFromCenter = Vector3.Distance(boxInTrunk.position, trunkPoint.position);
            if (distanceFromCenter > trunkBounds.magnitude * 1.5f)
            {
                boxInTrunk = null; // коробка выпала
            }
        }
    }

    // ------------------ ПОДБОР И ВЫКЛАДКА ------------------
    void HandlePickup()
    {
        if (heldBox == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Box"))
                {
                    if (boxInTrunk == hit.transform && !trunkOpen) continue; // нельзя взять из закрытого багажника
                    if (Input.GetKeyDown(interactKey))
                    {
                        PickupBox(hit.transform);
                        break;
                    }
                }
            }
        }
        else
        {
            if (!boxMovingToTrunk)
            {
                heldBox.position = holdPoint.position;
                heldBox.rotation = holdPoint.rotation;

                float distanceToTrunk = Vector3.Distance(transform.position, trunkInteractPoint.position);
                if (trunkOpen && distanceToTrunk <= trunkInteractRange && Input.GetKeyDown(interactKey))
                {
                    StartMovingBoxToTrunk();
                }
            }
        }

        float distToTrunk = Vector3.Distance(transform.position, trunkInteractPoint.position);
        if (distToTrunk <= trunkInteractRange && Input.GetKeyDown(trunkKey))
        {
            trunkOpen = !trunkOpen;
        }
    }

    void PickupBox(Transform box)
    {
        heldBox = box;
        Rigidbody rb = heldBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void StartMovingBoxToTrunk()
    {
        if (heldBox == null) return;
        boxMovingToTrunk = true;
    }

    // ------------------ ПЛАВНОЕ ДВИЖЕНИЕ В БАГАЖНИК ------------------
    void HandleBoxMovement()
    {
        if (boxMovingToTrunk && heldBox != null)
        {
            Rigidbody rb = heldBox.GetComponent<Rigidbody>();
            if (rb == null) return;

            // Плавное движение позиции к центру багажника
            heldBox.position = Vector3.SmoothDamp(heldBox.position, trunkPoint.position, ref boxVelocity, pullSmoothness, boxMoveSpeed);

            // Плавное вращение
            heldBox.rotation = Quaternion.Slerp(heldBox.rotation, trunkPoint.rotation, boxMoveSpeed * Time.fixedDeltaTime);

            // Если коробка почти в центре багажника
            if (Vector3.Distance(heldBox.position, trunkPoint.position) < 0.05f)
            {
                // Устанавливаем точно на место
                heldBox.position = trunkPoint.position;
                heldBox.rotation = trunkPoint.rotation;

                boxInTrunk = heldBox; // коробка теперь в багажнике

                // --- РЕГИСТРАЦИЯ В TrunkBoxPhysics ---
                if (trunkPhysics != null)
                {
                    Rigidbody rbInTrunk = boxInTrunk.GetComponent<Rigidbody>();
                    if (rbInTrunk != null)
                    {
                        trunkPhysics.RegisterBox(rbInTrunk);
                    }
                }

                heldBox = null;
                boxMovingToTrunk = false;
            }
        }
    }

    void ApplyTrunkSoftBounds()
    {
        if (boxInTrunk == null) return;

        Rigidbody rb = boxInTrunk.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 localPos = boxInTrunk.position - trunkPoint.position;
        Vector3 force = Vector3.zero;

        if (localPos.x > trunkBounds.x) force.x = (trunkBounds.x - localPos.x) * trunkSoftness;
        if (localPos.x < -trunkBounds.x) force.x = (-trunkBounds.x - localPos.x) * trunkSoftness;
        if (localPos.y > trunkBounds.y) force.y = (trunkBounds.y - localPos.y) * trunkSoftness;
        if (localPos.y < 0f) force.y = (0f - localPos.y) * trunkSoftness;
        if (localPos.z > trunkBounds.z) force.z = (trunkBounds.z - localPos.z) * trunkSoftness;
        if (localPos.z < -trunkBounds.z) force.z = (-trunkBounds.z - localPos.z) * trunkSoftness;

        rb.AddForce(force * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }

    void HandleTrunkAnimation()
    {
        if (trunkDoor == null) return;

        float targetAngle = trunkOpen ? trunkOpenAngle : 0f;
        trunkCurrentAngle = Mathf.MoveTowards(trunkCurrentAngle, targetAngle, trunkSpeed * 50f * Time.deltaTime);
        trunkCurrentAngle = Mathf.Clamp(trunkCurrentAngle, 0f, trunkOpenAngle);

        trunkDoor.localEulerAngles = new Vector3(-trunkCurrentAngle, 0f, 0f);
    }

    void HandleDrop()
    {
        if (heldBox != null && Input.GetKeyDown(dropKey))
        {
            Rigidbody rb = heldBox.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(transform.forward * dropForce, ForceMode.VelocityChange);
            }
            heldBox = null;
            boxMovingToTrunk = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        if (trunkInteractPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(trunkInteractPoint.position, trunkInteractRange);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(trunkPoint.position, trunkBounds * 2f);
    }
}
