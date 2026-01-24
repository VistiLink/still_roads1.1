using UnityEngine;

public class CarCamera2 : MonoBehaviour
{
    public GameObject Player;
    public GameObject Child;
    private float speedKmh = 100f;
    public float speed;
    private Rigidbody rb;

    private void Start()
    {
        rb = Player.GetComponent<Rigidbody>();
    }


    private void aWake()
    {
        Player = GameObject.FindGameObjectWithTag("Player");
        Child = gameObject.transform.Find("camera const").gameObject;
    }
    private void FixedUpdate()
    {
        speedKmh = rb.linearVelocity.magnitude * 3.6f;
        follow();
    }
    private void follow()
    {
        if (speed <= 23)
            speed = Mathf.Lerp(speed, speedKmh / 4, Time.deltaTime);
        else
            speed = 23;

        gameObject.transform.position = Vector3.Lerp(transform.position, Child.transform.position, Time.deltaTime * speed);
        gameObject.transform.LookAt(Player.transform.position);
    }
}