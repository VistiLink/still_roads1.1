using UnityEngine;

public class HeadlightsToggle : MonoBehaviour
{
    [Header("Headlights")]
    public Light[] headlights;

    private bool lightsOn = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            lightsOn = !lightsOn;

            foreach (Light light in headlights)
            {
                light.enabled = lightsOn;
            }
        }
    }
}
