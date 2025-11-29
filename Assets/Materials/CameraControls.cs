using UnityEngine;
using Cinemachine;

public class CameraControls : MonoBehaviour
{
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera mainCam;
    public CinemachineVirtualCamera phoneCam;

    [Header("Cam Tilt / Thruster System")]
    public bool isSteering = false;
    [Tooltip("Transform that Cinemachine virtual camera follows. We'll tilt this.")]
    public Transform camTiltTarget;
    public Transform movableObject;          // e.g., ship visual model or movement root
    public float moveSpeed = 5f;             // units per second
    public float tiltAmount = 10f;           // degrees of camera tilt (pitch/roll)
    public float tiltSpeed = 5f;             // how fast it tilts
    public float returnSpeed = 3f;           // how fast it recenters when no input

    [Header("Passive Shake Settings")]
    public float maxAmplitude = 0.5f;
    public float maxFrequency = 1.5f;
    public Transform blackHole;
    public Transform ship;
    public float maxShakeDistance = 500f;

    [Header("Triggered Shake Settings")]
    public float shakeDecay = 1.5f;
    public float triggeredShakeAmplitude = 0f;

    private bool isInPhone = false;
    private CinemachineBasicMultiChannelPerlin mainNoise;
    private CinemachineBasicMultiChannelPerlin phoneNoise;

    [Header("Spaghettification Settings")]
    public float spaghettifyStartDistance = 100f;
    public float spaghettifyFullDistance = 20f;
    public float maxFOV = 180f;
    public float maxNearClip = 10f;
    public CanvasGroup fadeCanvas;
    public float fadeSpeed = 0.5f;

    // --- Private ---
    private float currentTiltX;
    private float currentTiltZ;

    void Start()
    {
        if (mainCam)
        {
            mainNoise = mainCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            mainCam.Priority = 20;
        }

        if (phoneCam)
        {
            phoneNoise = phoneCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            phoneCam.Priority = 10;
        }

        // Safety: if camTiltTarget is unset, attempt to use the follow target from mainCam
        if (camTiltTarget == null)
        {
            camTiltTarget = GetFollowTargetFromActiveCam();
            if (camTiltTarget == null)
            {
                Debug.LogWarning("[CameraControls] camTiltTarget is not assigned and no active virtual camera has a Follow target. Add one in inspector for tilt to work.");
            }
        }
    }

    void Update()
    {
        HandleCameraSwitch();
        UpdatePassiveShake();
        UpdateTriggeredShake();
        UpdateSpaghettification();
        UpdateThrusterSystem();
    }

    void HandleCameraSwitch()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            isInPhone = !isInPhone;

            if (isInPhone)
            {
                phoneCam.Priority = 20;
                mainCam.Priority = 10;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                mainCam.Priority = 20;
                phoneCam.Priority = 10;
                Cursor.lockState = CursorLockMode.Locked;
            }

            // If camTiltTarget was null, try to refresh it from the newly active cam
            if (camTiltTarget == null)
            {
                camTiltTarget = GetFollowTargetFromActiveCam();
                if (camTiltTarget == null)
                    Debug.LogWarning("[CameraControls] active camera still has no Follow target. Assign camTiltTarget in inspector.");
            }
        }
    }

    // =====================
    // THRUSTER SYSTEM
    // =====================
    void UpdateThrusterSystem()
    {
        // Don't do anything when steering disabled
        if (!isSteering) return;
    
        // Input (WASD or arrow keys) — Unity's default axes
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // If there's no cam target to tilt, try to get it
        if (camTiltTarget == null)
        {
            camTiltTarget = GetFollowTargetFromActiveCam();
            if (camTiltTarget == null)
            {
                // nothing to tilt — early out to avoid NRE
                return;
            }
        }

        // Calculate target tilts
        float targetTiltX = v * tiltAmount;    // pitch (local X)
        float targetTiltZ = -h * tiltAmount;   // roll (local Z)

        // Smoothly update internal tilt values
        currentTiltX = Mathf.Lerp(currentTiltX, targetTiltX, Time.deltaTime * tiltSpeed);
        currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltZ, Time.deltaTime * tiltSpeed);

        // Build a local rotation using current tilt (preserve existing yaw of the target)
        Vector3 currentLocalEuler = camTiltTarget.localEulerAngles;
        // Convert to signed angles to avoid large jumps (localEulerAngles is 0..360)
        float signedYaw = SignedAngleFrom360(currentLocalEuler.y);
        Quaternion targetLocalRot = Quaternion.Euler(currentTiltX, signedYaw, currentTiltZ);

        // Apply rotation smoothly to localRotation
        camTiltTarget.localRotation = Quaternion.Slerp(camTiltTarget.localRotation, targetLocalRot, Time.deltaTime * tiltSpeed);

        // Move referenced object if any
        if (movableObject)
        {
           Vector3 inputDir = new Vector3(-h, 0, -v); // your input
Vector3 moveDir = movableObject.TransformDirection(inputDir).normalized;

movableObject.position += moveDir * moveSpeed * 3f * (GameManager.Instance.engineAlloc/50) * Time.deltaTime;

        }

        // If there is no input, ease the tilt values back to zero faster (returnSpeed)
        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
        {
            currentTiltX = Mathf.Lerp(currentTiltX, 0f, Time.deltaTime * returnSpeed);
            currentTiltZ = Mathf.Lerp(currentTiltZ, 0f, Time.deltaTime * returnSpeed);
        }
    }

    // Helper that returns the Follow target transform from the currently active virtual camera (main or phone)
    Transform GetFollowTargetFromActiveCam()
    {
        var activeCam = isInPhone ? phoneCam : mainCam;
        if (activeCam != null && activeCam.Follow != null)
            return activeCam.Follow;
        return null;
    }

    // Convert 0..360 to -180..180
    float SignedAngleFrom360(float angle360)
    {
        if (angle360 > 180f) return angle360 - 360f;
        return angle360;
    }

    // =====================
    // EXISTING SYSTEMS (unchanged logic)
    // =====================
    void UpdatePassiveShake()
    {
        if (!blackHole || !ship || !mainNoise) return;

        float dist = Vector3.Distance(blackHole.position, ship.position);
        float t = Mathf.Clamp01(1f - dist / maxShakeDistance);

        float passiveAmp = Mathf.Lerp(0f, maxAmplitude, t);
        float passiveFreq = Mathf.Lerp(0f, maxFrequency, t);

        var noise = isInPhone ? phoneNoise : mainNoise;
        if (noise)
        {
            noise.m_AmplitudeGain = passiveAmp + triggeredShakeAmplitude;
            noise.m_FrequencyGain = passiveFreq;
        }
    }

    void UpdateTriggeredShake()
    {
        if (triggeredShakeAmplitude > 0f)
        {
            triggeredShakeAmplitude = Mathf.MoveTowards(triggeredShakeAmplitude, 0f, Time.deltaTime * shakeDecay);
        }
    }

    public void TriggerShake(float strength)
    {
        triggeredShakeAmplitude = Mathf.Max(triggeredShakeAmplitude, strength);
    }

    void UpdateSpaghettification()
    {
        if (!blackHole || !ship) return;

        float dist = Vector3.Distance(blackHole.position, ship.position);
        // guard in case GameManager is null in tests
        if (GameManager.Instance != null)
            GameManager.Instance.distFromHorizon = dist;

        var activeCam = isInPhone ? phoneCam : mainCam;
        if (!activeCam) return;

        var lens = activeCam.m_Lens;

        if (dist < spaghettifyStartDistance)
        {
            float t = Mathf.InverseLerp(spaghettifyStartDistance, spaghettifyFullDistance, dist);
            float intensity = Mathf.Pow(t, 3f);

            lens.FieldOfView = Mathf.Lerp(70f, maxFOV, intensity);
          
            activeCam.m_Lens = lens;

            if (fadeCanvas)
                fadeCanvas.alpha = Mathf.MoveTowards(fadeCanvas.alpha, intensity, Time.deltaTime * fadeSpeed);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, 70f, Time.deltaTime * 1.5f);
            activeCam.m_Lens = lens;

            if (fadeCanvas)
                fadeCanvas.alpha = Mathf.MoveTowards(fadeCanvas.alpha, 0f, Time.deltaTime * fadeSpeed);
        }
    }
}
