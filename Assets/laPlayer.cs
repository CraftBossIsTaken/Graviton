using UnityEngine;

public class HologramBillboard : MonoBehaviour
{
    public Transform target; // usually the player's camera
    public float turnSpeed = 3f;        // how fast it faces the player
    public float flickerAmount = 1.5f;  // degrees of subtle hologram drift
    public float flickerSpeed = 5f;     // how fast the drift happens

    private Quaternion randomOffset;

    void LateUpdate()
    {
        if (target == null)
        {
            if (Camera.main != null)
                target = Camera.main.transform;
            else
                return;
        }

        // Calculate direction (Y axis only)
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        // FIX 1: flip facing direction (so it's not backward)
        Quaternion targetRotation = Quaternion.LookRotation(-direction);

        // FIX 2: subtle holographic flicker instead of wobble
        float flickerX = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.3f) * 2f - 1f;
        float flickerY = Mathf.PerlinNoise(Time.time * flickerSpeed, 1.7f) * 2f - 1f;
        Quaternion flicker = Quaternion.Euler(flickerX * flickerAmount, 0f, flickerY * flickerAmount);

        // Smoothly rotate toward player, with slight flicker applied
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation * flicker, Time.deltaTime * turnSpeed);
    }
}
