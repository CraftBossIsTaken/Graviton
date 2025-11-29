using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using StarterAssets;

public class ShipBreakManager : MonoBehaviour
{
    [Header("Breach / Visuals")]
    [Tooltip("Disabled by default. A mesh/VFX representing the opened hole (enable when breach happens).")]
    public GameObject breachHole;           // small mesh or empty parent for VFX; keep inactive by default
    public ParticleSystem breachVFX;        // optional VFX (sparks, wind)
    public AudioSource breachSfx;           // optional alarm / tear sound
    public Transform breachPoint;           // exact world position where the breach is (center of hole)

    [Header("Player (assign one or more)")]
    public Transform playerTransform;       // player root transform
    public Rigidbody playerRigidbody;       // optional: physics-style player
    public CharacterController playerCC;    // optional: CharacterController (StarterAssets uses this)
    public FirstPersonController fpc;       // optional: StarterAssets controller to disable during the sequence
    public Camera playerCamera;             // optional: camera to help aim/look at black hole
[Header("Dynamic Break Box")]
public Collider breakGrabBox;              
public float ripForce = 140f;              
public float maxRipDistance = 6f;          
public float massOfRippedPiece = 3f;       
public string shipPrefix = "SM";           // all ship pieces start with "SM"
List<Rigidbody> dynamicBreakRBs = new List<Rigidbody>();
    [Header("Black hole / look target")]
    public Transform blackHole;             // final place to glance toward (big dramatic object)

    [Header("Suction Settings")]
    public float suckDuration = 2.0f;       // total time for the gentle suck
    public float suckStrength = 60f;        // base force applied to the player (tweak)
    public float finalPullDuration = 0.9f;  // short, intense pull into the hole
    public float finalPullStrength = 220f;  // final pull force

    [Header("Props to pull (for drama)")]
    [Tooltip("LayerMask used to find nearby rigidbodies (e.g. 'Debris' layer).")]
    public LayerMask propLayerMask;
    public float propSearchRadius = 12f;
    public int maxPropsToAffect = 12;
    public float propSuckStrength = 90f;

    [Header("Rotation (optional)")]
    public Transform objectToRotate;        // object that will slowly face rotateTowards during the sequence
    public Transform rotateTowards;         // target to face
    public float rotateSpeed = 1.5f;

    [Header("UI / End")]
    public CanvasGroup fadeCanvasGroup;     // optional UI canvas group used for fade-to-black
    public float fadeDuration = 1.2f;
    public UnityEvent onPlayerDeath;        // optional hooks (show death screen, respawn, etc)

    // internal
    Collider[] overlapBuffer = new Collider[64];

    void Reset()
    {
        // try to auto-fill some fields if possible
        if (!playerTransform && fpc) playerTransform = fpc.transform;
    }

    void Awake()
    {
        // keep breach visuals hidden until we need them
        if (breachHole != null) breachHole.SetActive(false);
        if (breachVFX != null) breachVFX.Stop();
    }

    void Update()
    {
        // debug trigger
      //  if (Input.GetKeyDown(KeyCode.H))
       // {
       //     BreakShip();
       // }
    }

    /// <summary>Call this to trigger the breach cinematic / suction sequence.</summary>
    public void BreakShip()
    {
        // basic validation
        if (breachPoint == null)
        {
            Debug.LogWarning("ShipBreakManager: breachPoint not assigned.");
            return;
        }

        StartCoroutine(BreachSequence());
    }

    IEnumerator BreachSequence()
    {
        // 0) visuals + sound
        if (breachHole != null) breachHole.SetActive(true);
        if (breachVFX != null) breachVFX.Play();
        if (breachSfx != null) breachSfx.Play();

        // 1) disable player input/controller (if provided)
        if (fpc != null) fpc.enabled = false;

        // 2) find a few nearby rigidbodies (props) to pull for drama
        List<Rigidbody> props = GatherNearbyProps();

        // 3) gentle suction (player & props) — fallback for player types
        float t = 0f;
        while (t < suckDuration)
        {
            float normalized = t / suckDuration;              // 0 → 1
            float ease = 1f - Mathf.Pow(1f - normalized, 2f); // slightly ease-in

            // player
            ApplySuctionToPlayer(breachPoint.position, suckStrength * (0.5f + ease * 0.8f) * Time.deltaTime);

            // props
            ApplySuctionToProps(props, propSuckStrength * (0.5f + ease) * Time.deltaTime);
TryRipNearbyShipParts();

            // optional: rotate some object toward a target for drama
            RotateDuringSequence();

            t += Time.deltaTime;
            yield return null;
        }

        // 4) short intense pull into the breach (makes it feel decisive)
        float tf = 0f;
        Vector3 finalTarget = (blackHole != null) ? blackHole.position : breachPoint.position;
        while (tf < finalPullDuration)
        {
            float p = tf / finalPullDuration;
            float forceMultiplier = Mathf.SmoothStep(1f, 0f, 1f - p); // ramp up slightly
            ApplySuctionToPlayer(finalTarget, finalPullStrength * Time.deltaTime * forceMultiplier);
            ApplySuctionToProps(props, propSuckStrength * 1.8f * Time.deltaTime * forceMultiplier);

            RotateDuringSequence();

            tf += Time.deltaTime;
            yield return null;
        }

        // 5) fade / finalization
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));
        }

        // 6) notify death / re-enable if you want to respawn
        onPlayerDeath?.Invoke();

        // NOTE: we intentionally do NOT re-enable fpc here. Let your death handler decide.
    }

    List<Rigidbody> GatherNearbyProps()
    {
        List<Rigidbody> list = new List<Rigidbody>(8);

        // OverlapSphere to find colliders (cheap-ish for one-time use)
        int count = Physics.OverlapSphereNonAlloc(breachPoint.position, propSearchRadius, overlapBuffer, propLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count && list.Count < maxPropsToAffect; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;
            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                list.Add(rb);
            }
        }

        return list;
    }

    void ApplySuctionToProps(List<Rigidbody> props, float perFrameStrength)
    {
        if (props == null) return;
        foreach (var rb in props)
        {
            if (rb == null) continue;
            Vector3 dir = (breachPoint.position - rb.position);
            float dist = Mathf.Max(0.5f, dir.magnitude);
            Vector3 force = dir.normalized * (perFrameStrength / dist); // falloff by distance
            rb.AddForce(force, ForceMode.Acceleration);
        }
    }
void TryRipNearbyShipParts()
{
    if (breakGrabBox == null) return;

    // Overlap the break box using a 3D OverlapBox
    Collider[] hits = Physics.OverlapBox(
        breakGrabBox.bounds.center,
        breakGrabBox.bounds.extents,
        breakGrabBox.transform.rotation,
        ~0, // all layers
        QueryTriggerInteraction.Ignore
    );

    foreach (var h in hits)
    {
        if (h == null) continue;

        Transform t = h.transform;

        // check prefix (ship pieces start with SM)
        if (!t.name.StartsWith(shipPrefix)) continue;

        // already ripped?
        if (t.GetComponent<Rigidbody>()) continue;

        // ensure a collider exists
        if (!t.GetComponent<Collider>())
        {
            t.gameObject.AddComponent<BoxCollider>();
        }

        // add RigidBody
        Rigidbody rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = massOfRippedPiece;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        dynamicBreakRBs.Add(rb);

        // apply rip force toward breach point
        Vector3 dir = (breachPoint.position - t.position);
        float dist = dir.magnitude;

        if (dist < maxRipDistance)
        {
            rb.AddForce(dir.normalized * ripForce, ForceMode.Impulse);
        }
    }
}

void OnDrawGizmosSelected()
{
    if (breakGrabBox != null)
    {
        Gizmos.color = new Color(0, 0.5f, 1f, 0.25f);
        Gizmos.DrawCube(breakGrabBox.bounds.center, breakGrabBox.bounds.size);
    }
}

    void ApplySuctionToPlayer(Vector3 target, float perFrameStrength)
    {
        if (playerTransform == null) return;

        Vector3 dir = (target - playerTransform.position);
        float dist = Mathf.Max(0.25f, dir.magnitude);
        Vector3 forceDir = dir.normalized;

        // If player has a Rigidbody: use AddForce for a physical pull
        if (playerRigidbody != null)
        {
            playerRigidbody.AddForce(forceDir * (perFrameStrength / dist), ForceMode.Acceleration);
            // optional: also add a small torque for tumbling feel (only if desired)
        }
        // CharacterController (StarterAssets) -> move the controller
        else if (playerCC != null)
        {
            // convert to a simple velocity movement
            Vector3 move = forceDir * (perFrameStrength * 0.02f); // scale down for CC
            playerCC.Move(move * Time.deltaTime * 60f); // normalized for frame rate
        }
        // Fallback: directly lerp transform (non-physical)
        else
        {
            float step = (perFrameStrength * Time.deltaTime) / Mathf.Max(1f, dist * 0.5f);
            playerTransform.position = Vector3.Lerp(playerTransform.position, playerTransform.position + forceDir * 1f, step);
        }

        // try to force camera to face the black hole for that "one last look"
        if (playerCamera != null && blackHole != null)
        {
            Vector3 lookDir = (blackHole.position - playerCamera.transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRot, 3f * Time.deltaTime);
        }
    }

    void RotateDuringSequence()
    {
        if (objectToRotate == null || rotateTowards == null) return;

        Vector3 dir = rotateTowards.position - objectToRotate.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        objectToRotate.rotation = Quaternion.Slerp(objectToRotate.rotation, target, rotateSpeed * Time.deltaTime);
    }

    IEnumerator FadeCanvas(float from, float to, float dur)
    {
        if (fadeCanvasGroup == null) yield break;
        float t = 0f;
        fadeCanvasGroup.blocksRaycasts = true;
        while (t < dur)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}
