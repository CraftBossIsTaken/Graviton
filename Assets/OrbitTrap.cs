using UnityEngine;

public class OrbitTrap : MonoBehaviour
{
    [Header("Orbit Trap Settings")]
    public float trapDuration = 10f;
    public float orbitSpeed = 40f;
    public float radialLockStrength = 4f;
    public float slingshotForce = 20f;

    Rigidbody rb;
    bool trapped = false;
    float trapTimer = 0f;
    Transform orbitParent;
    Vector3 orbitAxis = Vector3.up;
    float orbitRadius;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!trapped) return;

        trapTimer += Time.fixedDeltaTime;

        // 1. Force ship to orbit around the parent
        Vector3 center = orbitParent.position;

        // direction tangent to orbit, BUT reverse (opposite asteroid direction)
        Vector3 toShip = transform.position - center;
        Vector3 tangent = Vector3.Cross(orbitAxis, toShip).normalized;

        rb.velocity = tangent * orbitSpeed;

        // 2. Pull ship toward orbit radius (keep distance stable)
        float currentDist = toShip.magnitude;
        float delta = orbitRadius - currentDist;

        rb.AddForce(toShip.normalized * delta * radialLockStrength, ForceMode.Acceleration);

        // 3. Release?
        if (trapTimer >= trapDuration)
            ExitOrbit();
    }

    /// <summary>
    /// Call this when the ship touches an orbit ring.
    /// orbitParent = the rotating parent GameObject of the orbit.
    /// </summary>
    public void EnterOrbit(Transform orbitParentTransform, Vector3 axis, float radius)
    {
        trapped = true;
        trapTimer = 0f;

        orbitParent = orbitParentTransform;
        orbitAxis = axis.normalized;
        orbitRadius = radius;

        // Optional: zero velocity for clean capture
        rb.velocity = Vector3.zero;

        Debug.Log("Ship entered orbit!");
    }

    void ExitOrbit()
    {
        trapped = false;

        // Slingshot = apply forward impulse
        rb.AddForce(transform.forward * slingshotForce, ForceMode.Impulse);

        Debug.Log("Ship escaped orbit via slingshot!");
    }
}
