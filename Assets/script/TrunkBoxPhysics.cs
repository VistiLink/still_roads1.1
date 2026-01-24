using UnityEngine;
using System.Collections.Generic;

public class TrunkBoxPhysics : MonoBehaviour
{
    [Header("References")]
    public Transform trunkPoint;      // центр багажника
    public Vector3 trunkBounds = new Vector3(1f, 0.5f, 1.5f); // половина размеров багажника
    public Rigidbody carRigidbody;    // Rigidbody машины

    [Header("Physics Settings")]
    public float softPullForce = 5f;      // сила притягивания к центру багажника
    public float safeFallGravity = 2f;    // уменьшение падения в багажнике

    private List<Rigidbody> boxesInTrunk = new List<Rigidbody>();  // все зарегистрированные коробки

    void FixedUpdate()
    {
        if (boxesInTrunk.Count == 0) return;

        foreach (var boxRb in boxesInTrunk.ToArray())
        {
            if (boxRb == null)
            {
                boxesInTrunk.Remove(boxRb);
                continue;
            }

            Vector3 localPos = boxRb.position - trunkPoint.position;

            // Проверяем, если коробка внутри безопасной зоны багажника
            bool insideSafeZone = Mathf.Abs(localPos.x) <= trunkBounds.x &&
                                  Mathf.Abs(localPos.y) <= trunkBounds.y &&
                                  Mathf.Abs(localPos.z) <= trunkBounds.z;

            if (insideSafeZone)
            {
                // уменьшение падения
                boxRb.AddForce(Vector3.up * Physics.gravity.y * (1 - safeFallGravity) * -1f, ForceMode.Acceleration);
                // мягкое притягивание к центру
                Vector3 pull = -localPos * softPullForce * Time.fixedDeltaTime;
                boxRb.AddForce(pull, ForceMode.VelocityChange);
            }
            else
            {
                // коробка вышла за пределы — больше не притягиваем
                boxesInTrunk.Remove(boxRb);
            }

            // проверка переворота машины (наклон более 70°)
            if (Vector3.Dot(carRigidbody.transform.up, Vector3.up) < 0.3f)
            {
                boxesInTrunk.Remove(boxRb);
            }
        }
    }

    // Метод для регистрации коробки в багажнике
    public void RegisterBox(Rigidbody box)
    {
        if (!boxesInTrunk.Contains(box) && box != null)
        {
            boxesInTrunk.Add(box);
        
        }
    }
}