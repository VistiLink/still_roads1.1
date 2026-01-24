using UnityEngine;

public class CarEnterExit : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public GameObject playerCamera;
    public GameObject carCamera;
    public MonoBehaviour playerMovement;
    public MonoBehaviour carController;
    public Transform exitPosition;
    public Rigidbody carRigidbody;

    [Header("Sounds")]
    public AudioSource enterCarSound;   // звук входа
    public AudioSource exitCarSound;    // звук выхода

    private bool playerInCar = true;
    private bool playerNearCar = false;

    private AudioSource[] carAudioSources;

    void Start()
    {
        carAudioSources = GetComponentsInChildren<AudioSource>();

        // Изначально игрок в машине
        player.SetActive(false);
        playerCamera.SetActive(false);
        carCamera.SetActive(true);

        playerMovement.enabled = false;
        carController.enabled = true;

        MuteCarAudio(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (playerInCar)
                ExitCar();
            else if (playerNearCar)
                EnterCar();
        }
    }

    void ExitCar()
    {
        playerInCar = false;

        // 🔊 звук выхода
        if (exitCarSound != null)
            exitCarSound.Play();

        if (exitPosition != null)
            player.transform.position = exitPosition.position;
        else
            player.transform.position = transform.position + transform.right * 2f;

        player.SetActive(true);
        playerMovement.enabled = true;
        carController.enabled = false;

        playerCamera.SetActive(true);
        carCamera.SetActive(false);

        // 🔇 глушим звуки машины
        MuteCarAudio(true);
    }

    void EnterCar()
    {
        playerInCar = true;

        // 🔊 звук входа
        if (enterCarSound != null)
            enterCarSound.Play();

        player.SetActive(false);
        playerMovement.enabled = false;
        carController.enabled = true;

        playerCamera.SetActive(false);
        carCamera.SetActive(true);

        // 🔊 включаем звуки машины (без Play)
        MuteCarAudio(false);
    }

    void MuteCarAudio(bool mute)
    {
        if (carAudioSources == null) return;

        foreach (AudioSource audio in carAudioSources)
        {
            // ❗ не глушим звуки входа/выхода
            if (audio == enterCarSound || audio == exitCarSound)
                continue;

            audio.mute = mute;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearCar = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearCar = false;
    }
}
