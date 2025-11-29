using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrusterController_Debug : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    [Tooltip("Where 'forward' is defined for steering (place at cockpit/front). If null, will use this.transform.")]
    public Transform steeringOrigin;
    [Tooltip("Pivot used to tilt the camera for visual feedback. Optional.")]
    public Transform cameraPivot;

    [Header("VFX (optional)")]
    public ParticleSystem leftThrusterVFX;
    public ParticleSystem rightThrusterVFX;
    public ParticleSystem frontThrusterVFX;
    public ParticleSystem backThrusterVFX;

    [Header("Forces & Energy")]
    public float impulseForce = 12f;
    public float impulseUpForce = 0f;
    public float energy = 100f;
    public float maxEnergy = 100f;
    public float energyCostPerBurst = 15f;
    public float energyRechargePerSecond = 6f;
    public float burstCooldown = 0.18f;

    [Header("Rotation & Feedback")]
    public bool allowRotation = true;
    [Tooltip("Degrees to tilt camera on strong lateral thrust. Positive tilts 'forward' nose down.")]
    public float cameraTiltAngle = 8f;
    public float cameraTiltDamping = 6f;
    public float angularDampingWhenSettling = 2f;

    // internals
    private float lastBurstTime = -10f;
    private float targetCameraTilt = 0f;
    private float currentCameraTilt = 0f;

    private struct PendingImpulse { public Vector3 force; public Vector3 worldPoint; public ParticleSystem vfx; public float cameraTilt; }
    private List<PendingImpulse> pendingImpulses = new List<PendingImpulse>(8);

    void Reset() { rb = GetComponent<Rigidbody>(); }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (steeringOrigin == null) steeringOrigin = transform;
        // Safety checks:
        if (rb == null) Debug.LogError("[ThrusterController_Debug] No Rigidbody found!");
        if (rb != null && rb.isKinematic) Debug.LogWarning("[ThrusterController_Debug] Rigidbody is kinematic — thrusters won't move it.");
    }

    void Update()
    {
        RechargeEnergy();
        // Input: use GetKeyDown for single bursts or GetKey for repeated (change to preference)
        if (Input.GetKeyDown(KeyCode.A)) TryFireLeft();
        if (Input.GetKeyDown(KeyCode.D)) TryFireRight();
        if (Input.GetKeyDown(KeyCode.W)) TryFireFront();
        if (Input.GetKeyDown(KeyCode.S)) TryFireBack();

        // camera smoothing
        currentCameraTilt = Mathf.Lerp(currentCameraTilt, targetCameraTilt, Time.deltaTime * cameraTiltDamping);
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(currentCameraTilt, 0f, 0f);
        }
        // decay target
        targetCameraTilt = Mathf.MoveTowards(targetCameraTilt, 0f, Time.deltaTime * cameraTiltDamping * 2f);
    }

    void FixedUpdate()
    {
        if (pendingImpulses.Count == 0) return;
        // apply all pending impulses in this physics step
        foreach (var p in pendingImpulses)
        {
            if (rb == null) break;

            if (allowRotation)
            {
                rb.AddForceAtPosition(p.force, p.worldPoint, ForceMode.Impulse);
                Debug.Log($"[Thruster] Applied impulse {p.force} at {p.worldPoint} (rotational allowed).");
            }
            else
            {
                rb.AddForce(p.force, ForceMode.Impulse);
                Debug.Log($"[Thruster] Applied central impulse {p.force} (rotation disabled).");
            }

            if (p.vfx != null) p.vfx.Play();
            targetCameraTilt = p.cameraTilt;
            // lightweight settle damping
            StartCoroutine(TemporaryAngularDamping());
        }
        pendingImpulses.Clear();
    }

    void RechargeEnergy()
    {
        if (energy < maxEnergy)
        {
            energy += energyRechargePerSecond * Time.deltaTime;
            if (energy > maxEnergy) energy = maxEnergy;
        }
    }

    #region Attempt-fire wrappers
    public void TryFireLeft() { TryFire(Vector3.left, leftThrusterVFX, cameraTilt: cameraTiltAngle); }
    public void TryFireRight() { TryFire(Vector3.right, rightThrusterVFX, cameraTilt: -cameraTiltAngle); }
    public void TryFireFront() { TryFire(Vector3.forward, frontThrusterVFX, cameraTilt: 0f); }
    public void TryFireBack() { TryFire(Vector3.back, backThrusterVFX, cameraTilt: 0f); }
    #endregion

    void TryFire(Vector3 localDir, ParticleSystem vfx, float cameraTilt)
    {
        if (Time.time - lastBurstTime < burstCooldown)
        {
            Debug.Log("[Thruster] Burst on cooldown.");
            return;
        }
        if (energy < energyCostPerBurst)
        {
            Debug.Log("[Thruster] Not enough energy to fire.");
            return;
        }
        lastBurstTime = Time.time;
        energy -= energyCostPerBurst;

        Vector3 worldDir = steeringOrigin.TransformDirection(localDir.normalized);
        Vector3 force = worldDir * impulseForce + steeringOrigin.TransformDirection(Vector3.up) * impulseUpForce;
        Vector3 applicationPoint = CalculateThrusterApplicationPoint(localDir);

        // queue for FixedUpdate
        pendingImpulses.Add(new PendingImpulse { force = force, worldPoint = applicationPoint, vfx = vfx, cameraTilt = cameraTilt });

        Debug.Log($"[Thruster] Queued {localDir} burst. Energy left: {energy:F1}. Force: {force}. ApplyPoint: {applicationPoint}");
    }

    Vector3 CalculateThrusterApplicationPoint(Vector3 localDirection)
    {
        float lateralOffset = 1.0f;
        float forwardOffset = 0.5f;
        Vector3 localPoint = Vector3.zero;
        if (localDirection == Vector3.left) localPoint = new Vector3(-lateralOffset, 0f, forwardOffset);
        else if (localDirection == Vector3.right) localPoint = new Vector3(lateralOffset, 0f, forwardOffset);
        else if (localDirection == Vector3.forward) localPoint = new Vector3(0f, 0f, forwardOffset);
        else if (localDirection == Vector3.back) localPoint = new Vector3(0f, 0f, -forwardOffset);
        return steeringOrigin.TransformPoint(localPoint);
    }

    IEnumerator TemporaryAngularDamping()
    {
        if (rb == null) yield break;
        float originalAngularDrag = rb.angularDrag;
        rb.angularDrag += angularDampingWhenSettling;
        yield return new WaitForSeconds(0.16f);
        rb.angularDrag = originalAngularDrag;
    }

    // Editor gizmos to visualize steering origin and thruster points
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform so = steeringOrigin ? steeringOrigin : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(so.position, so.position + so.forward * 1.0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(CalculateThrusterApplicationPoint(Vector3.left), 0.06f);
        Gizmos.DrawSphere(CalculateThrusterApplicationPoint(Vector3.right), 0.06f);
        Gizmos.DrawSphere(CalculateThrusterApplicationPoint(Vector3.forward), 0.06f);
        Gizmos.DrawSphere(CalculateThrusterApplicationPoint(Vector3.back), 0.06f);
    }
#endif
}
