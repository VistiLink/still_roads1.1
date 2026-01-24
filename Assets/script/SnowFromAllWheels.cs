using UnityEngine;

public class SnowFromAllWheels : MonoBehaviour
{
    public ParticleSystem snowParticles;
    public Rigidbody carRb;

    public float maxEmission = 80f;
    public float maxSpeed = 60f;

    void Start()
    {
        if (snowParticles == null)
            snowParticles = GetComponent<ParticleSystem>();

        if (carRb == null)
            carRb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (snowParticles == null || carRb == null)
            return;

        float speedKmh = carRb.linearVelocity.magnitude * 3.6f;

        // ✅ ПРАВИЛЬНО получаем модуль
        var emission = snowParticles.emission;

        float t = Mathf.Clamp01(speedKmh / maxSpeed);
        emission.rateOverTime = Mathf.Lerp(0f, maxEmission, t);
    }
}
