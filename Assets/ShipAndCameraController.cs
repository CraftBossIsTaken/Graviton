using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipAndCameraController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform shipModel;

    [Header("Movement")]
    public float thrustPower = 30f;
    public float sideThrusterPower = 20f;
    public float linearDamping = 1f;

    [Header("Rotation")]
    public float yawSpeed = 90f;

    [Header("Tilt")]
    public float maxTiltDegrees = 75f;
    public float tiltSmooth = 8f;
    public KeyCode tiltLeft = KeyCode.Q;
    public KeyCode tiltRight = KeyCode.E;

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0, 4, -10);
    public float cameraPosSmooth = 0.08f;
    public float cameraRotSmooth = 8f;

    Rigidbody rb;
    Vector3 camVel;

    float tiltCurrent;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (shipModel == null)
            Debug.LogWarning("Assign shipModel (child mesh) or tilt will affect physics.");
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleYaw();
        ApplyDamping();
        HandleCamera();   // FOLLOW PHYSICS -> NO JITTER
    }

    void Update()
    {
        HandleTiltVisual();
    }

    // ------------------------ MOVEMENT ------------------------

    void HandleMovement()
    {
        float f = Input.GetAxis("Vertical");
        if (Mathf.Abs(f) > 0.01f)
            rb.AddRelativeForce(Vector3.forward * (f * thrustPower), ForceMode.Acceleration);

        float side = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) side -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) side += 1f;

        if (side != 0f)
            rb.AddRelativeForce(Vector3.right * (side * sideThrusterPower), ForceMode.Acceleration);
    }

    void HandleYaw()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) > 0.01f)
        {
            float deg = h * yawSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, deg, 0));
        }
    }

    void ApplyDamping()
    {
        if (linearDamping <= 0f) return;

        Vector3 lv = rb.velocity;
        lv = Vector3.Lerp(lv, Vector3.zero, linearDamping * Time.fixedDeltaTime);
        rb.velocity = lv;
    }

    // ------------------------ VISUAL TILT ------------------------

    void HandleTiltVisual()
    {
        if (shipModel == null) return;

        float t = 0f;
        if (Input.GetKey(tiltLeft)) t = maxTiltDegrees;
        else if (Input.GetKey(tiltRight)) t = -maxTiltDegrees;

        tiltCurrent = Mathf.Lerp(tiltCurrent, t, Time.deltaTime * tiltSmooth);

        Vector3 e = shipModel.localEulerAngles;
        e.z = tiltCurrent;
        shipModel.localEulerAngles = e;
    }

    // ------------------------ CAMERA ------------------------

    void HandleCamera()
    {
        if (cameraTransform == null) return;

        Vector3 targetPos = transform.TransformPoint(cameraOffset);

        cameraTransform.position =
            Vector3.SmoothDamp(
                cameraTransform.position,
                targetPos,
                ref camVel,
                cameraPosSmooth
            );

        Vector3 targetDir = transform.position - cameraTransform.position;
        if (targetDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
            cameraTransform.rotation =
                Quaternion.Slerp(cameraTransform.rotation, targetRot, Time.fixedDeltaTime * cameraRotSmooth);
        }
    }
}
