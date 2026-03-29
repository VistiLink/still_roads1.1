using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class RearWheelDrive : MonoBehaviour
{
    [Header("Car Settings")]
    public float maxAngle = 50f;
    public float maxTorque = 300f;
    public float maxWheelRPM = 1000f; // Лимит оборотов, чтобы не крутились слишком быстро

    [Header("Handbrake")]
    public float handbrakeTorque = 6000f;
    public float handbrakeExtraDamping = 3f;
    public float handbrakeSmoothTime = 0.25f;

    [Header("Grip")]
    public float forwardStiffness = 1.5f;
    public float sidewaysStiffness = 2.0f;

    [Header("Wheel Models")]
    public GameObject frontLeftWheelModel;
    public GameObject frontRightWheelModel;
    public GameObject rearLeftWheelModel;
    public GameObject rearRightWheelModel;

    [Header("Flip Car")]
    public KeyCode flipKey1 = KeyCode.A;
    public KeyCode flipKey2 = KeyCode.D;
    public float flipForceUp = 3000f;
    public float flipTorque = 1500f;

    [Header("Handbrake UI")]
    public UIDocument handbrakeUIDocument;
    private Image handbrakeImage;
    public Texture2D handbrakeOffTexture;
    public Texture2D handbrakeOnTexture;

    [Header("Handbrake Sound")]
    public AudioSource handbrakeAudio;
    public AudioClip handbrakeClip;

    [Header("Tire Sounds")]
    public AudioSource tireAudioNormal;
    public AudioSource tireAudioSnow;

    [Header("Engine Sound")]
    public AudioSource engineAudio;
    public AudioClip engineClip;
    public float engineStartSpeed = 25f;
    public float engineVolume = 0.8f;
    public float maxEnginePitch = 1.6f;

    [Header("Snow Settings")]
    public string snowTag = "Snow";
    public float snowDamping = 2f;

    private WheelCollider[] wheels;
    private Rigidbody rb;
    private float speedKmh;
    private float defaultDamping;
    private bool tireOnSnow = false;

    private bool handbrakeOn = false;
    private Coroutine handbrakeCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Фикс зацепа: опускаем центр масс, чтобы колеса не висели в воздухе
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        wheels = GetComponentsInChildren<WheelCollider>();
        defaultDamping = rb.linearDamping;

        foreach (WheelCollider wheel in wheels)
        {
            GameObject model = null;

            if (wheel.transform.localPosition.z > 0)
                model = wheel.transform.localPosition.x < 0 ? frontLeftWheelModel : frontRightWheelModel;
            else
                model = wheel.transform.localPosition.x < 0 ? rearLeftWheelModel : rearRightWheelModel;

            if (model != null)
            {
                GameObject w = Instantiate(model, wheel.transform);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
            }
        }

        if (handbrakeUIDocument != null)
        {
            var root = handbrakeUIDocument.rootVisualElement;
            handbrakeImage = root.Q<Image>("HandbrakeIcon");
            if (handbrakeImage != null)
                handbrakeImage.image = handbrakeOffTexture;
        }

        if (engineAudio != null && engineClip != null)
        {
            engineAudio.clip = engineClip;
            engineAudio.loop = true;
            engineAudio.volume = 0f;
            engineAudio.pitch = 0.9f;
            engineAudio.Play();
        }

        if (tireAudioNormal != null)
        {
            tireAudioNormal.loop = true;
            tireAudioNormal.volume = 0f;
            tireAudioNormal.Play();
        }

        if (tireAudioSnow != null)
        {
            tireAudioSnow.loop = true;
            tireAudioSnow.volume = 0f;
            tireAudioSnow.Play();
        }
    }

    void Update()
    {
        speedKmh = rb.linearVelocity.magnitude * 3.6f;

        if (!MenuinGame.BlockSpaceInput && Input.GetKeyDown(KeyCode.Space))
        {
            handbrakeOn = !handbrakeOn;

            if (handbrakeCoroutine != null)
                StopCoroutine(handbrakeCoroutine);

            handbrakeCoroutine = handbrakeOn
                ? StartCoroutine(HandbrakeOnRoutine())
                : StartCoroutine(HandbrakeOffRoutine());

            if (handbrakeImage != null)
                handbrakeImage.image = handbrakeOn ? handbrakeOnTexture : handbrakeOffTexture;

            if (handbrakeAudio != null && handbrakeClip != null)
                handbrakeAudio.PlayOneShot(handbrakeClip);
        }

        float angle = maxAngle * Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        float torque = maxTorque * verticalInput;

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel.transform.localPosition.z > 0)
                wheel.steerAngle = angle;

            if (wheel.transform.localPosition.z < 0)
            {
                if (!handbrakeOn)
                {
                    // Проверка на RPM: если колесо крутится быстрее лимита, не даем больше момента
                    if (Mathf.Abs(wheel.rpm) < maxWheelRPM)
                        wheel.motorTorque = torque;
                    else
                        wheel.motorTorque = 0f;

                    wheel.brakeTorque = 0f;
                }
                else
                {
                    // Если ручник включен, мотор не должен крутить колеса
                    wheel.motorTorque = 0f;
                }

                // Сброс момента, если педаль газа отпущена
                if (Mathf.Abs(verticalInput) < 0.05f)
                    wheel.motorTorque = 0f;

                ApplyGrip(wheel);
            }

            if (wheel.transform.childCount > 0)
            {
                wheel.GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheel.transform.GetChild(0).SetPositionAndRotation(pos, rot);
            }
        }

        UpdateSnowPhysics();
        UpdateTireSound();
        UpdateEngineSound();

        if ((Input.GetKeyDown(flipKey1) || Input.GetKeyDown(flipKey2)) && IsUpsideDown())
            FlipCar();
    }

    IEnumerator HandbrakeOnRoutine()
    {
        float t = 0f;
        float startDrag = rb.linearDamping;

        while (t < handbrakeSmoothTime)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / handbrakeSmoothTime);

            foreach (WheelCollider wheel in wheels)
            {
                if (wheel.transform.localPosition.z < 0)
                    wheel.brakeTorque = Mathf.Lerp(0f, handbrakeTorque, f);
            }

            rb.linearDamping = Mathf.Lerp(startDrag, handbrakeExtraDamping, f);
            yield return null;
        }
    }

    IEnumerator HandbrakeOffRoutine()
    {
        float t = 0f;
        float startDrag = rb.linearDamping;

        while (t < handbrakeSmoothTime)
        {
            t += Time.deltaTime;
            float f = 1f - Mathf.Clamp01(t / handbrakeSmoothTime);

            foreach (WheelCollider wheel in wheels)
            {
                if (wheel.transform.localPosition.z < 0)
                    wheel.brakeTorque = Mathf.Lerp(0f, handbrakeTorque, f);
            }

            rb.linearDamping = Mathf.Lerp(startDrag, defaultDamping, 1f - f);
            yield return null;
        }

        foreach (WheelCollider wheel in wheels)
            wheel.brakeTorque = 0f;

        rb.linearDamping = defaultDamping;
    }

    void UpdateSnowPhysics()
    {
        tireOnSnow = false;

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel.GetGroundHit(out WheelHit hit) && hit.collider.CompareTag(snowTag))
            {
                rb.linearDamping = snowDamping;
                tireOnSnow = true;
                return;
            }
        }

        rb.linearDamping = defaultDamping;
    }

    void UpdateTireSound()
    {
        float normalTarget = tireOnSnow ? 0f : (speedKmh > 2f ? 0.6f : 0f);
        float snowTarget = tireOnSnow ? 0.6f : 0f;
        float pitch = Mathf.Lerp(0.8f, 1.2f, speedKmh / 60f);

        if (tireAudioNormal != null)
        {
            tireAudioNormal.volume = Mathf.Lerp(tireAudioNormal.volume, normalTarget, Time.deltaTime * 5f);
            tireAudioNormal.pitch = pitch;
        }

        if (tireAudioSnow != null)
        {
            tireAudioSnow.volume = Mathf.Lerp(tireAudioSnow.volume, snowTarget, Time.deltaTime * 5f);
            tireAudioSnow.pitch = pitch;
        }
    }

    void UpdateEngineSound()
    {
        if (engineAudio == null) return;

        if (speedKmh < engineStartSpeed)
        {
            engineAudio.volume = Mathf.Lerp(engineAudio.volume, 0f, Time.deltaTime * 3f);
            return;
        }
        engineAudio.volume = Mathf.Lerp(engineAudio.volume, engineVolume, Time.deltaTime * 3f);
        engineAudio.pitch = Mathf.Lerp(0.9f, maxEnginePitch, speedKmh / 120f);
    }

    void ApplyGrip(WheelCollider wheel)
    {
        WheelFrictionCurve f = wheel.forwardFriction;
        WheelFrictionCurve s = wheel.sidewaysFriction;

        f.stiffness = forwardStiffness;
        s.stiffness = sidewaysStiffness;

        wheel.forwardFriction = f;
        wheel.sidewaysFriction = s;
    }

    bool IsUpsideDown()
    {
        return Vector3.Dot(transform.up, Vector3.down) > 0.7f;
    }

    void FlipCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(Vector3.up * flipForceUp, ForceMode.Impulse);
        rb.AddTorque((transform.forward + transform.right).normalized * flipTorque, ForceMode.Impulse);
    }
}