using UnityEngine;
using Cinemachine;

/// <summary>
/// Gives a “heavy” manual rotation feel to a camera, similar to operating a large mounted weapon.
/// Rotation only runs when a specified CinemachineVirtualCamera is disabled.
///
/// Attach this script to the camera you want to control manually.
/// </summary>
[RequireComponent(typeof(Camera))]
public class otherCamRot : MonoBehaviour
{
    [Header("Dependencies")]
    public CinemachineVirtualCamera checkVirtualCamera; // Rotation active only when this is disabled

    [Header("Rotation Settings")]
    public float rotationSpeed = 30f;          // base speed in degrees/second
    public float heavySmoothing = 4f;          // larger = heavier, slower to follow input
    public float maxPitch = 80f;               // clamp up/down
    public float minPitch = -40f;

    [Header("Input Settings")]
    public string horizontalAxis = "Mouse X";
    public string verticalAxis = "Mouse Y";

    [Header("Optional Settings")]
    public bool invertY = true;
    public bool useTimeScale = true;

    private Vector2 targetRotation; // desired yaw/pitch
    private Vector2 smoothRotation; // actual smoothed rotation

    void Start()
    {
        // Initialize rotations from current camera
        Vector3 euler = transform.localEulerAngles;
        targetRotation = new Vector2(euler.y, euler.x);
        smoothRotation = targetRotation;
    }

    void Update()
    {
        // Check if rotation should be active
        if (checkVirtualCamera != null && checkVirtualCamera.enabled)
        
            return;
    if(GetComponent<CinemachineVirtualCamera>().enabled == false)
            return;

        float dt = useTimeScale ? Time.deltaTime : Time.unscaledDeltaTime;

        // Get input
        float inputX = Input.GetAxis(horizontalAxis);
        float inputY = Input.GetAxis(verticalAxis) * (invertY ? 1f : -1f);

        // Update target rotation based on input
        targetRotation.x += inputX * rotationSpeed * dt;
        targetRotation.y += inputY * rotationSpeed * dt;

        // Clamp pitch
        targetRotation.y = Mathf.Clamp(targetRotation.y, minPitch, maxPitch);

        // Smoothly interpolate (heavy feel)
        smoothRotation = Vector2.Lerp(smoothRotation, targetRotation, dt * heavySmoothing);

        // Apply rotation
        transform.localRotation = Quaternion.Euler(smoothRotation.y, smoothRotation.x, 0f);
    }

    /// <summary>
    /// Instantly reset rotation to default or to match Cinemachine camera if desired.
    /// </summary>
    public void ResetRotation(Vector3? newAngles = null)
    {
        Vector3 euler = newAngles ?? Vector3.zero;
        targetRotation = new Vector2(euler.y, euler.x);
        smoothRotation = targetRotation;
        transform.localRotation = Quaternion.Euler(euler);
    }
}
