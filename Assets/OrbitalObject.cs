// OrbitalObject.cs (small snippet)
using System;
using UnityEngine;

public class OrbitalObject : MonoBehaviour
{
    public static event Action<float> OnGlobalImpulse;
    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void OnEnable() => OnGlobalImpulse += HandleImpulse;
    void OnDisable() => OnGlobalImpulse -= HandleImpulse;

    void HandleImpulse(float magnitude)
    {
        if (rb == null) return;
        Vector3 dir = (transform.position - BlackHoleRetreat.Instance.transform.position).normalized;
        Vector3 impulse = dir * magnitude * (rb.mass + 0.1f);
        rb.AddForce(impulse, ForceMode.VelocityChange);
    }
    public static void TriggerGlobalImpulse(float magnitude)
{
    OnGlobalImpulse?.Invoke(magnitude);
}

}
