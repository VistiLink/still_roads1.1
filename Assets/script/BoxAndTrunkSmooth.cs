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
    public float dropForce = 2f;             // сила выброса коробки

    [Header("Effects")]
    public ParticleSystem teleportEffect;    // эффект при телепортации в багажник
    public AudioClip teleportSound;          // звук при телепортации
    public float soundVolume = 1f;           // громкость звука

    [Header("Trunk Physics")]
    public TrunkBoxPhysics trunkPhysics;     // ссылка на скрипт физики багажника

    private Transform heldBox = null;        // коробка в руках
    private bool trunkOpen = false;
    private float trunkCurrentAngle = 0f;
    private Transform boxInTrunk = null;     // отслеживание, что коробка в багажнике

    private AudioSource audioSource;         // аудио источник

    void Awake()
    {
        // Создаём или получаем AudioSource на объекте
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        HandlePickup();
        HandleTrunkAnimation();
        HandleDrop();
    }

    void FixedUpdate()
    {
        // Если коробка ушла далеко — сбрасываем статус "в багажнике"
        if (boxInTrunk != null)
        {
            float distanceFromCenter = Vector3.Distance(boxInTrunk.position, trunkPoint.position);
            if (distanceFromCenter > 5f) // просто большое число, чтобы сбросить
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
            heldBox.position = holdPoint.position;
            heldBox.rotation = holdPoint.rotation;

            float distanceToTrunk = Vector3.Distance(transform.position, trunkInteractPoint.position);
            if (trunkOpen && distanceToTrunk <= trunkInteractRange && Input.GetKeyDown(interactKey))
            {
                TeleportBoxToTrunk();
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

    void TeleportBoxToTrunk()
    {
        if (heldBox == null) return;

        // Телепорт коробки
        heldBox.position = trunkPoint.position;
        heldBox.rotation = trunkPoint.rotation;

        // Воспроизведение эффекта
        if (teleportEffect != null)
        {
            ParticleSystem effect = Instantiate(teleportEffect, trunkPoint.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration + effect.main.startLifetime.constantMax);
        }

        // Воспроизведение звука
        if (teleportSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(teleportSound, soundVolume);
        }

        boxInTrunk = heldBox;

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
    }

    // ------------------ ДВИЖЕНИЕ ДВЕРИ БАГАЖНИКА ------------------
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
    }
}
