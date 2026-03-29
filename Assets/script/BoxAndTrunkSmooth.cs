using UnityEngine;

// --- ОСНОВНОЙ КЛАСС ИГРОКА ---
public class BoxAndTrunkSmooth : MonoBehaviour
{
    [Header("References")]
    public Transform holdPoint;
    public Transform trunkPoint;
    public Transform trunkDoor;
    public Transform trunkInteractPoint;
    public Rigidbody carRigidbody;

    [Header("Settings")]
    public float pickupRange = 2f;
    public float trunkInteractRange = 2f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode trunkKey = KeyCode.F;
    public KeyCode dropKey = KeyCode.G;
    public float trunkOpenAngle = 70f;
    public float trunkSpeed = 2f;
    public float dropForce = 2f;

    [Header("Effects")]
    public ParticleSystem teleportEffect; // Эффект телепортации в багажник
    public ParticleSystem destroyEffect;  // ЭФФЕКТ ИСЧЕЗНОВЕНИЯ КОРОБКИ
    public AudioClip teleportSound;
    public float soundVolume = 1f;

    [Header("Trunk Physics")]
    public TrunkBoxPhysics trunkPhysics;

    [Header("Secondary Box Settings")]
    public float secondaryFollowHeight = 0.5f;
    public float secondaryDestroyDelay = 0.1f;

    private Transform heldBox = null;
    private Transform secondaryBox = null;
    private Rigidbody secondaryBoxRb = null;

    private bool trunkOpen = false;
    private float trunkCurrentAngle = 0f;
    private Transform boxInTrunk = null;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        HandlePickup();
        HandleTrunkAnimation();
        HandleDrop();
    }

    void FixedUpdate()
    {
        if (boxInTrunk != null)
        {
            if (Vector3.Distance(boxInTrunk.position, trunkPoint.position) > 5f)
                boxInTrunk = null;
        }
    }

    void HandlePickup()
    {
        if (heldBox == null)
        {
            if (Input.GetKeyDown(interactKey))
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange);
                foreach (var hit in hits)
                {
                    if (hit.CompareTag("Box"))
                    {
                        if (boxInTrunk == hit.transform && !trunkOpen) continue;
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

            if (trunkOpen && Vector3.Distance(transform.position, trunkInteractPoint.position) <= trunkInteractRange)
            {
                if (Input.GetKeyDown(interactKey)) TeleportBoxToTrunk();
            }
        }

        if (Vector3.Distance(transform.position, trunkInteractPoint.position) <= trunkInteractRange)
        {
            if (Input.GetKeyDown(trunkKey)) trunkOpen = !trunkOpen;
        }
    }

    void PickupBox(Transform box)
    {
        heldBox = box;
        Rigidbody rb = heldBox.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Collider[] colliders = Physics.OverlapBox(box.position + Vector3.up * 0.5f, box.localScale);
        foreach (var col in colliders)
        {
            if (col.CompareTag("ExtraBox"))
            {
                secondaryBox = col.transform;
                secondaryBoxRb = secondaryBox.GetComponent<Rigidbody>();

                Vector3 relativePos = box.InverseTransformPoint(secondaryBox.position);
                Quaternion relativeRot = Quaternion.Inverse(box.rotation) * secondaryBox.rotation;

                secondaryBox.SetParent(heldBox);
                secondaryBox.localPosition = relativePos;
                secondaryBox.localRotation = relativeRot;

                secondaryBoxRb.isKinematic = true;

                SecondaryBoxLogic logic = secondaryBox.GetComponent<SecondaryBoxLogic>();
                if (logic != null) logic.inCar = false;

                break;
            }
        }
    }

    void TeleportBoxToTrunk()
    {
        if (heldBox == null) return;

        heldBox.position = trunkPoint.position;
        heldBox.rotation = trunkPoint.rotation;

        Rigidbody mainRb = heldBox.GetComponent<Rigidbody>();
        if (mainRb != null) mainRb.isKinematic = false;

        if (teleportEffect != null) Instantiate(teleportEffect, trunkPoint.position, Quaternion.identity);
        if (teleportSound != null) audioSource.PlayOneShot(teleportSound, soundVolume);

        boxInTrunk = heldBox;
        if (trunkPhysics != null && mainRb != null) trunkPhysics.RegisterBox(mainRb);

        if (secondaryBox != null)
        {
            secondaryBox.SetParent(null);
            secondaryBoxRb.isKinematic = false;

            SecondaryBoxLogic logic = secondaryBox.GetComponent<SecondaryBoxLogic>();
            if (logic == null) logic = secondaryBox.gameObject.AddComponent<SecondaryBoxLogic>();

            // Передаем настройки и ЭФФЕКТ в логику коробки
            logic.destroyDelay = secondaryDestroyDelay;
            logic.effectPrefab = destroyEffect;
            logic.inCar = true;

            secondaryBox = null;
            secondaryBoxRb = null;
        }

        heldBox = null;
    }

    void HandleDrop()
    {
        if (heldBox != null && Input.GetKeyDown(dropKey))
        {
            Rigidbody rb = heldBox.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(transform.forward * dropForce, ForceMode.Impulse);
            }

            if (secondaryBox != null)
            {
                secondaryBox.SetParent(null);
                secondaryBoxRb.isKinematic = false;
            }

            heldBox = null;
            secondaryBox = null;
        }
    }

    void HandleTrunkAnimation()
    {
        if (trunkDoor == null) return;
        float targetAngle = trunkOpen ? trunkOpenAngle : 0f;
        trunkCurrentAngle = Mathf.MoveTowards(trunkCurrentAngle, targetAngle, trunkSpeed * 100f * Time.deltaTime);
        trunkDoor.localEulerAngles = new Vector3(-trunkCurrentAngle, 0f, 0f);
    }
}

// --- ЛОГИКА ВТОРОЙ КОРОБКИ (С ЭФФЕКТОМ) ---
public class SecondaryBoxLogic : MonoBehaviour
{
    public float destroyDelay;
    public ParticleSystem effectPrefab;
    public bool inCar = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (inCar && collision.gameObject.layer == LayerMask.NameToLayer("groundbox"))
        {
            // Создаем эффект перед удалением
            if (effectPrefab != null)
            {
                ParticleSystem eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
                eff.Play();
                Destroy(eff.gameObject, 2f); // Удаляем сам эффект через пару секунд
            }

            Destroy(gameObject, destroyDelay);
        }
    }
}