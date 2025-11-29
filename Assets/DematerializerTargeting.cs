using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
/// <summary>
/// Attach this to the Weapon GameObject (or a manager on the weapon).
/// Responsibilities:
/// - Continuously scans for the nearest target by tags (default: "PowerSource").
/// - When not aiming (isAiming == false) the weapon will auto-track the nearest target (smooth look-at).
/// - When the player begins aiming (set isAiming = true) auto-tracking pauses and a yellow reticle UI is shown at
///   the target's screen position. If the target stays inside the aim-center tolerance for lockHoldTime seconds,
///   it "locks" (reticle turns green).
/// - After locking, a simple alignment minigame runs: a rotating needle must be stopped inside a sweet spot by calling
///   AttemptFire() (you should hook this to your fire input). If successful, OnDematerializeSuccess is invoked.
/// - Flexible: supply different targetTags (e.g. "Asteroid") to reuse for other weapon modes.
///
/// Required setup in the Inspector:
/// - weaponTransform: the transform to rotate when auto-tracking (can be the weapon model or the whole weapon object).
/// - cameraRef: camera used for screen-space conversions (defaults to Camera.main).
/// - targetCanvas: UI canvas (Screen Space - Overlay or Camera) where the reticle will be spawned.
/// - reticlePrefab: a simple UI prefab (RectTransform) containing an Image. The script will tint it yellow/green.
///   If you don't want to make a sprite: create an Image with a hollow circle sprite, set its Image type to Simple,
///   set "Raycast Target" to false.
/// - alignmentPrefab (optional): a small UI prefab used for the alignment minigame. If omitted the script creates a
///   simple rotating image using the reticle as a placeholder.
///
/// How "hold over it" works:
/// - The center of aim is considered the screen center. When isAiming==true, if the target's screen position is within
///   aimTolerancePixels of screen center, the hold timer increases. Holding for lockHoldTime seconds locks the target.
///
/// Hook points:
/// - OnLock (UnityEvent) — called when target locks.
/// - OnDematerializeSuccess / OnDematerializeFail — called after the alignment minigame when AttemptFire is used.
///
/// You can call ResetLock() to clear lock and restart.
///
/// Example usage: call AttemptFire() when player presses the fire button while locked.
/// </summary>
public class DematerializerTargeting : MonoBehaviour
{
    
    [Header("References")]
    public otherCamRot ocr;
    public AudioSource fireSound;
    public Transform weaponTransform; // the part to rotate when auto-tracking
    public Camera cameraRef;
    public Camera cameraWeaponRef;
    public Canvas targetCanvas;
    public RectTransform reticlePrefab; // prefab must be a UI element (Image) that can be tinted
    public RectTransform alignmentPrefab; // optional alignment UI (needle + sweet spot) - can be null
[Header("Minigame Prefabs")]
public RectTransform needlePrefab;
public RectTransform backgroundPrefab;
public RectTransform sweetSpotPrefab;
private bool _hadTargetLastFrame = false;
public Interact it;
    [Header("Targeting")]
    public string[] targetTags = new string[] { "PowerSource" };
    public float scanRange = 2000f;
    public float autoRotateSpeed = 5f; // how fast the weapon auto-rotates to face target

    [Header("Aiming / Lock")]
    [Tooltip("External flag: set true when player is aiming (e.g. right mouse down). The script will stop auto-tracking then.")]
    public bool isAiming = false;
    public float aimTolerancePixels = 60f; // how close to center the target must be to count as "holding over"
    public float lockHoldTime = 3f;

 [Header("Rotation Sync Minigame")]
public float satelliteDriftSpeed = 30f;  // degrees per second
public float playerAdjustSpeed = 50f;    // how fast player can change their matching rate
public float syncThreshold = 5f;         // acceptable degrees/sec difference for success
public float syncDuration = 2f;          // how long to hold within threshold to succeed

private float _satelliteRotRate;
private float _playerRotRate;
private float _syncTimer;
[Header("Camera Lock-On")]
public bool autoCameraLock = true;        // Enable camera to follow target after lock
public float cameraFollowSpeed = 5f;      // Smooth speed for camera rotation
private Transform originalCameraParent;    // to restore camera if needed
private Quaternion originalCameraLocalRot; 

    [Header("Misc")]
    public string lockTintProperty = "_Color"; // if you use a material-based UI, not used by default

    [Header("Events")]
    public UnityEvent OnLock;
    public UnityEvent OnDematerializeSuccess;
    public UnityEvent OnDematerializeFail;
[Header("Beam/Particle")]
public GameObject beamPrefab; // assign your particle system prefab
public Transform beamSpawnPoint; // where the particle starts
public float beamTravelTime = 0.2f; // time to reach the target
    // runtime
    Transform currentTarget;
    RectTransform _reticleInstance;
    RectTransform _alignmentInstance;
    Image _reticleImage;

    float _holdTimer = 0f;
    bool _locked = false;
    bool _minigameActive = false;

    // alignment needle state
    RectTransform _needleRT;
    float _needleAngle = 0f; // 0-360
[Header("UI Text Indicators")]
public TextMeshProUGUI lockText;       // Displays "Lock" or similar
public TextMeshProUGUI minigameText;   // Displays "Calibrate" or similar
public Slider calibrationSlider;
private Vector3 _currentVelocity;

    float baseScanRange;
    float baseAutoRotateSpeed;
    float basePlayerAdjustSpeed;
    float baseSatelliteDriftSpeed;
    float baseAimTolerance;
    float baseLockHoldTime;
    float baseSyncThreshold;
    float baseSyncDuration;
    float baseCameraFollowSpeed;

    // last known allocations (to avoid recalculating unnecessarily)
    float prevEngineAlloc = -1f;
    float prevShieldsAlloc = -1f;
    float prevSensorsAlloc = -1f;
    float prevDeflectorsAlloc = -1f;
    float prevAutomationAlloc = -1f;

    // last computed normalized fractions for use elsewhere
    float ef, shf, sf, df, af;
void SetLockTextColor(Color c)
{
    if (lockText) lockText.color = c;
}

void SetMinigameTextColor(Color c)
{
    if (minigameText) minigameText.color = c;
}
void UpdateCalibrationSlider(float value)
{
    if (calibrationSlider)
        calibrationSlider.value = Mathf.Clamp01(value);
}

void LateUpdate()
{
    if (_locked && autoCameraLock && currentTarget != null)
    {
        ocr.enabled = false;
        Vector3 dir = (currentTarget.position - cameraWeaponRef.transform.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

        // Smoothly rotate using Quaternion.RotateTowards or Slerp with damping
        cameraWeaponRef.transform.rotation = Quaternion.Slerp(
            cameraWeaponRef.transform.rotation,
            targetRot,
            1 - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime)
        );
    }
    else
    {
        ocr.enabled = true;
    }
}

  void Start()
{
    if (cameraRef == null) cameraRef = Camera.main;
    if (weaponTransform == null) weaponTransform = this.transform;
      baseScanRange = scanRange;
        baseAutoRotateSpeed = autoRotateSpeed;
        basePlayerAdjustSpeed = playerAdjustSpeed;
        baseSatelliteDriftSpeed = satelliteDriftSpeed;
        baseAimTolerance = aimTolerancePixels;
        baseLockHoldTime = lockHoldTime;
        baseSyncThreshold = syncThreshold;
        baseSyncDuration = syncDuration;
        baseCameraFollowSpeed = cameraFollowSpeed;
    // Ensure weapon starts at the desired X (pitch) baseline and locked Z (roll = 180).
    // Keep the current yaw (Y).
    Vector3 cur = weaponTransform.localEulerAngles;
    // Normalize X to -180..180 so we can set a negative pitch cleanly
    float curY = cur.y;
    weaponTransform.localRotation = Quaternion.Euler(-50.869f, curY, 180f);
    ApplyAllocations();
}


 void Update()
{
    bool hadTargetThisFrame = currentTarget != null;
 ApplyAllocationsIfNeeded();
    // Detect when a new satellite appears
    if (!_hadTargetLastFrame && hadTargetThisFrame)
    {
        // Only now enable the reticle (if aiming)
        if (isAiming)
            EnsureReticleExists();
    }

    // Detect when target is lost
    if (_hadTargetLastFrame && !hadTargetThisFrame)
    {
        // Disable/hide the reticle
        DestroyReticle();
    }

    // Update tracking state
    _hadTargetLastFrame = hadTargetThisFrame;

    // --- existing targeting logic continues here ---
    if (currentTarget == null)
        currentTarget = FindNearestTarget();
    else
    {
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > scanRange || !currentTarget.gameObject.activeInHierarchy)
        {
            ClearTarget();
        }
    }
 if (currentTarget != null)
        {
            AutoRotateToTarget(currentTarget);
        }
    if (!isAiming)
    {
        if (_reticleInstance != null) DestroyReticle(); // hide UI while not aiming
    
        _holdTimer = 0f;
        _locked = false;
        _minigameActive = false;
    }
    else // isAiming == true
    {
        if (currentTarget != null)
        {
            // Only show if a satellite is detected
            if (_reticleInstance == null)
                EnsureReticleExists();

            UpdateReticlePosition();
            ProcessHoldToLock();
        }
        else
        {
            // If aiming but no satellite detected
            DestroyReticle();
            currentTarget = FindNearestTarget();
        }
    }

    if (_minigameActive)
        UpdateAlignmentNeedle();
}


    Transform FindNearestTarget()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var tag in targetTags)
        {
            GameObject[] arr = GameObject.FindGameObjectsWithTag(tag);
            for (int i = 0; i < arr.Length; i++)
            {
                var go = arr[i];
                if (!go.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d < bestDist && d <= scanRange)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }
        }

        return best;
    }

    void ClearTarget()
    {
        currentTarget = null;
        _holdTimer = 0f;
        _locked = false;
        if (_reticleInstance) DestroyReticle();
    }
void AutoRotateToTarget(Transform t)
{
    if (t == null) return;

    Vector3 dir = (t.position - weaponTransform.position).normalized;
    if (dir.sqrMagnitude < 0.0001f) return;

    // Compute the raw look rotation toward the target
    Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
    Vector3 lookEuler = lookRot.eulerAngles;

    // Convert lookEuler.x to -180..180 range so we can clamp negative pitch values correctly
    float rawPitch = lookEuler.x;
    if (rawPitch > 180f) rawPitch -= 360f; // now in -180..180

    // Clamp pitch to the desired range around -50.869
    float minPitch = -50.869f - 2f;
    float maxPitch = -50.869f + 2f;
    float clampedPitch = Mathf.Clamp(rawPitch, minPitch, maxPitch);

    // Yaw (heading) we allow to follow the target
    float yaw = lookEuler.y;

    // Fixed roll of 180
    float roll = 180f;

    // Build desired rotation and smoothly slerp to it
    Quaternion desired = Quaternion.Euler(clampedPitch, yaw, roll);
    weaponTransform.rotation = Quaternion.Slerp(weaponTransform.rotation, desired, Time.deltaTime * autoRotateSpeed);
}



    void EnsureReticleExists()
    {
        if (_reticleInstance != null) return;
        if (reticlePrefab == null || targetCanvas == null)
        {
            Debug.LogWarning("Reticle prefab or target canvas not assigned on DematerializerTargeting.");
            return;
        }
        _reticleInstance = Instantiate(reticlePrefab, targetCanvas.transform, false);
        _reticleImage = _reticleInstance.GetComponent<Image>();
        if (_reticleImage == null)
        {
            Debug.LogWarning("Reticle prefab needs an Image component to tint colors.");
        }

        // initialize color to yellow
        SetReticleYellow();

        // instantiate alignment if provided (hidden initially)
        if (alignmentPrefab != null)
        {
            _alignmentInstance = Instantiate(alignmentPrefab, targetCanvas.transform, false);
            _alignmentInstance.gameObject.SetActive(false);
            // try find needle
            _needleRT = _alignmentInstance.Find("Needle") as RectTransform;
        }
    }

    void DestroyReticle()
    {
        if (_reticleInstance) Destroy(_reticleInstance.gameObject);
        _reticleInstance = null;
        _reticleImage = null;
        if (_alignmentInstance) Destroy(_alignmentInstance.gameObject);
        _alignmentInstance = null;
        _needleRT = null;
        _minigameActive = false;
        _holdTimer = 0f;
        _locked = false;
    }
void UpdateReticlePosition()
{
    if (_reticleInstance == null || currentTarget == null) return;

    Vector3 screenPos = cameraRef.WorldToScreenPoint(currentTarget.position);

    // hide if behind camera
    if (screenPos.z < 0f)
    {
        _reticleInstance.gameObject.SetActive(false);
        if (_alignmentInstance != null) _alignmentInstance.gameObject.SetActive(false);
        return;
    }

    _reticleInstance.gameObject.SetActive(true);

    // convert to local canvas coords
    Vector2 anchoredPos;
    RectTransform canvasRT = targetCanvas.transform as RectTransform;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
        screenPos,
        targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cameraRef,
        out anchoredPos);
    _reticleInstance.anchoredPosition = anchoredPos;

    if (_alignmentInstance != null)
        _alignmentInstance.anchoredPosition = anchoredPos + new Vector2(0, -40);

    // --- Keep a constant on-screen pixel size ---
    // Desired pixel size on screen:
    float desiredPixelSize = 64f; // tweak this to taste

    if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
    {
        // overlay canvas: UI units are in pixels (modulo scaleFactor)
        float scaleFactor = Mathf.Max(targetCanvas.scaleFactor, 1f);
        _reticleInstance.sizeDelta = Vector2.one * (desiredPixelSize / scaleFactor);
    }
    else if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera)
    {
        // ScreenSpace-Camera: canvas has a scaleFactor and sits at a plane distance.
        // We convert desired pixels -> world size at current target depth, then set sizeDelta in canvas units.
        // First compute world height at target depth: height = 2 * z * tan(fov/2)
        float z = Mathf.Max(screenPos.z, 0.01f);
        float worldScreenHeight = 2f * z * Mathf.Tan(cameraRef.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float pixelsPerWorldUnit = Screen.height / worldScreenHeight;
        // desired world size that would correspond to desiredPixelSize
        float desiredWorldSize = desiredPixelSize / Mathf.Max(pixelsPerWorldUnit, 0.0001f);

        // Now we must map that world size into canvas units. Canvas plane distance can affect mapping.
        // A convenient method: convert two world points separated by desiredWorldSize into canvas local points,
        // measure the difference and set sizeDelta accordingly.
        Vector3 worldCenter = cameraRef.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        Vector3 worldRight = cameraRef.ScreenToWorldPoint(new Vector3(screenPos.x + desiredPixelSize, screenPos.y, z));
        Vector3 worldUp = cameraRef.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y + desiredPixelSize, z));

        Vector2 localCenter, localRight, localUp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, cameraRef.WorldToScreenPoint(worldCenter), cameraRef, out localCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, cameraRef.WorldToScreenPoint(worldRight), cameraRef, out localRight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, cameraRef.WorldToScreenPoint(worldUp), cameraRef, out localUp);

        float dx = Vector2.Distance(localCenter, localRight);
        float dy = Vector2.Distance(localCenter, localUp);
        // choose average to set a square size
        float canvasUnitsSize = (dx + dy) * 0.5f;
        if (canvasUnitsSize <= 0.001f) canvasUnitsSize = desiredPixelSize / Mathf.Max(targetCanvas.scaleFactor, 1f);

        _reticleInstance.sizeDelta = Vector2.one * canvasUnitsSize;
    }
    else // WorldSpace
    {
        // WorldSpace canvas: easiest reliable method is make the reticle a child of the canvas and
        // figure the world-space scale that maps to desiredPixelSize.
        float z = Mathf.Max(screenPos.z, 0.01f);
        float worldScreenHeight = 2f * z * Mathf.Tan(cameraRef.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float pixelsPerWorldUnit = Screen.height / worldScreenHeight;
        float desiredWorldSize = desiredPixelSize / Mathf.Max(pixelsPerWorldUnit, 0.0001f);

        // Assume the reticle prefab's rect transform has a size in its `rect` (in local canvas units).
        Vector2 baseRectSize = _reticleInstance.rect.size;
        if (baseRectSize.x <= 0.001f) baseRectSize = Vector2.one * 100f;

        float worldUnitsPerCanvasUnit = desiredWorldSize / (baseRectSize.x / targetCanvas.scaleFactor);
        _reticleInstance.localScale = Vector3.one * worldUnitsPerCanvasUnit;
    }
}


    void ProcessHoldToLock()
    {
        if (_reticleInstance == null || _reticleImage == null) return;

        // check distance from target screen pos to center of screen
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector3 targetScreen = cameraRef.WorldToScreenPoint(currentTarget.position);
        if (targetScreen.z < 0)
        {
            // behind camera
            _holdTimer = 0f;
            return;
        }

        float d = Vector2.Distance(screenCenter, new Vector2(targetScreen.x, targetScreen.y));
        if (d <= aimTolerancePixels)
        {
            _holdTimer += Time.deltaTime;
            // optional: scale reticle a bit to indicate progress
            float t = Mathf.Clamp01(_holdTimer / lockHoldTime);
            _reticleInstance.localScale = Vector3.one * (1f + 0.4f * t);
            if (!_locked && _holdTimer >= lockHoldTime)
{
    Debug.Log("locked on");
    _locked = true;
    SetReticleGreen();
    SetLockTextColor(Color.green); // ✅ turns Lock text green
    OnLock?.Invoke();
    StartAlignmentMinigame();
}

            
        }
        else
        {
            _holdTimer = 0f;
            _reticleInstance.localScale = Vector3.one;
            if (_locked)
            {
                // you moved off after lock — keep locked by default or reset? we keep locked until ResetLock called
            }
        }
    }

    void SetReticleYellow()
    {
        if (_reticleImage) _reticleImage.color = Color.yellow;
    }
    void SetReticleGreen()
    {
        if (_reticleImage) _reticleImage.color = Color.green;
    }
public void FadeReticleToTransparency()
{
    if (_reticleImage != null)
        StartCoroutine(FadeReticleCoroutine(1f)); // fade over 1 second
}

private IEnumerator FadeReticleCoroutine(float duration)
{
    float elapsed = 0f;
    Color startColor = _reticleImage.color;
    Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        _reticleImage.color = Color.Lerp(startColor, endColor, elapsed / duration);
        yield return null;
    }

    _reticleImage.color = endColor;
}

void StartAlignmentMinigame()
{

_minigameActive = true;
SetMinigameTextColor(Color.red);
UpdateCalibrationSlider(0f); // reset slider
    _needleAngle = 0f;
    _satelliteRotRate = Random.Range(-satelliteDriftSpeed, satelliteDriftSpeed);
    _playerRotRate = 0f;
    _syncTimer = 0f;

    // Fade reticle out after locking
    FadeReticleToTransparency();

    if (_alignmentInstance == null)
    {
        _alignmentInstance = new GameObject("AlignmentUI").AddComponent<RectTransform>();
        _alignmentInstance.SetParent(targetCanvas.transform, false);
        _alignmentInstance.sizeDelta = new Vector2(120, 120);
    }

    // Background
    if (backgroundPrefab != null)
    {
        var bg = Instantiate(backgroundPrefab, _alignmentInstance);
        bg.gameObject.SetActive(true);
    }
    else
    {
        var bgGO = new GameObject("Ring");
        bgGO.transform.SetParent(_alignmentInstance, false);
        var bg = bgGO.AddComponent<Image>();
        bg.rectTransform.sizeDelta = new Vector2(120, 120);
        bg.color = new Color(0, 0, 0, 0.5f);
    }

    // Sweet spot
    if (sweetSpotPrefab != null)
    {
        var spot = Instantiate(sweetSpotPrefab, _alignmentInstance);
    }
    else
    {
        var spotGO = new GameObject("SweetSpot");
        spotGO.transform.SetParent(_alignmentInstance, false);
        var spot = spotGO.AddComponent<Image>();
        spot.rectTransform.anchoredPosition = new Vector2(0, 30);
        spot.rectTransform.sizeDelta = new Vector2(40, 20);
        spot.color = new Color(0, 1, 0, 0.6f);
    }

    // Needle
    if (needlePrefab != null)
    {
        _needleRT = Instantiate(needlePrefab, _alignmentInstance);
        _needleRT.gameObject.SetActive(true);
    }
    else
    {
        var needleGO = new GameObject("Needle");
        needleGO.transform.SetParent(_alignmentInstance, false);
        _needleRT = needleGO.AddComponent<RectTransform>();
        _needleRT.sizeDelta = new Vector2(6, 60);
        _needleRT.anchoredPosition = Vector2.zero;

        var needleImg = needleGO.AddComponent<Image>();
        needleImg.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        needleImg.color = Color.yellow;
    }

    _alignmentInstance.gameObject.SetActive(true);
}


void UpdateAlignmentNeedle()
{
    float input = 0f;
    if (Input.GetKey(KeyCode.D)) input += 1f;
    if (Input.GetKey(KeyCode.A)) input -= 1f;

    // --- Passive drift only when no input ---
    if (Mathf.Abs(input) < 0.01f)
    {
        // Only drift if not touching controls
        _needleAngle += _satelliteRotRate * Time.deltaTime * 4f;
    }
    else
    {
        // Player input fully overrides drift
        _needleAngle += input * playerAdjustSpeed * Time.deltaTime;
    }

    // Normalize the angle
    _needleAngle = (_needleAngle % 360f + 360f) % 360f;

    // Apply rotation to UI
    if (_needleRT != null)
        _needleRT.localRotation = Quaternion.Euler(0, 0, -_needleAngle);

    // --- Evaluate sync ---
    float diff = Mathf.DeltaAngle(_needleAngle, 0f); // compare against 0 or target sweet spot
    bool inSync = Mathf.Abs(diff) <= syncThreshold;

    if (_needleRT != null)
        _needleRT.GetComponent<Image>().color = inSync ? Color.green : Color.yellow;

    // --- Success timer ---
    if (inSync)
    {
        _syncTimer += Time.deltaTime;
        UpdateCalibrationSlider(_syncTimer / syncDuration);
        if (_syncTimer >= syncDuration)
        {
            _minigameActive = false;
            SetMinigameTextColor(Color.green);
            UpdateCalibrationSlider(1f);
            OnDematerializeSuccess?.Invoke();
            FireBeamAtTarget();
            ResetLock();
            it.ReturnToNormalCamera(2f);
            float baseEnergy = Random.Range(15f, 20f);
            float energyMultiplier = 1f + 0.8f * shf + 0.4f * af + 0.2f * df;
            float energyToAdd = baseEnergy * energyMultiplier;
            GameManager.Instance.addBackupPower(energyToAdd);
        }
    }
    else
    {
        _syncTimer = Mathf.Max(0f, _syncTimer - Time.deltaTime * 0.5f);
        UpdateCalibrationSlider(_syncTimer / syncDuration);
    }
}





public void AttemptFire()
{
    // allow manual completion attempt instead of automatic success
    if (!_locked || !_minigameActive) return;

    float diff = Mathf.Abs(_playerRotRate - _satelliteRotRate);
    bool success = diff <= syncThreshold * 0.5f; // must be very close if firing manually

    if (success)
    {
        OnDematerializeSuccess?.Invoke();
        FireBeamAtTarget();
    }
    else
    {
        OnDematerializeFail?.Invoke();
        Debug.Log("Dematerializer alignment failed — rotation mismatch.");
    }

    _minigameActive = false;
    ResetLock();
}

void FireBeamAtTarget()
{
    if (currentTarget == null) return;

    // --- 1) Visual beam effect ---
    if (beamPrefab != null && beamSpawnPoint != null)
    {
        GameObject beamInstance = Instantiate(beamPrefab, beamSpawnPoint.position, Quaternion.identity);
StartCoroutine(CameraShake(cameraWeaponRef.transform, 0.15f, 0.1f));
        Vector3 targetPos = currentTarget.position;
        fireSound.Play();
        Vector3 startPos = beamSpawnPoint.position;
        Vector3 velocity = (targetPos - startPos) / beamTravelTime;
Debug.Log($"Firing beam at {currentTarget.name}, spawn point: {beamSpawnPoint.position}");

        Rigidbody rb = beamInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = velocity;
        }
        else
        {
            StartCoroutine(MoveBeamCoroutine(beamInstance.transform, startPos, targetPos, beamTravelTime));
            
        }

    
    }

    // --- 2) Dissolve the target ---
    Transform meshChild = currentTarget.GetComponentInChildren<MeshRenderer>()?.transform;
    if (meshChild == null) meshChild = currentTarget;

    var dissolver = GetComponent<Dissolver>();
    if (dissolver != null)
    {
        dissolver.AnimateDissolve(false, meshChild.gameObject, 3f);
    }
    else
    {
        Debug.LogWarning("No Dissolver component found on Dematerializer.");
    }

    // --- 3) Debug / optional line ---
    Debug.DrawLine(beamSpawnPoint.position, currentTarget.position, Color.cyan, 2f);
}
IEnumerator MoveBeamCoroutine(Transform beam, Vector3 start, Vector3 end, float duration)
{
    float elapsed = 0f;
    while (elapsed < duration)
    {
        if (beam == null) yield break;
        beam.position = Vector3.Lerp(start, end, elapsed / duration);
        elapsed += Time.deltaTime;
        yield return null;
    }
    if (beam != null) beam.position = end;
    yield return new WaitForSeconds(1.2f);
    Destroy(beam.gameObject);
}


public IEnumerator CameraShake(Transform cam, float duration, float magnitude)
{
    Vector3 originalPos = cam.localPosition;
    float elapsed = 0f;

    while (elapsed < duration)
    {
        float x = Random.Range(-1f, 1f) * magnitude;
        float y = Random.Range(-1f, 1f) * magnitude;
        cam.localPosition = originalPos + new Vector3(x, y, 0);
        elapsed += Time.deltaTime;
        yield return null;
    }

    cam.localPosition = originalPos;
}
 public void ResetLock()
{
    _holdTimer = 0f;
    if (_reticleInstance != null) SetReticleYellow();
    if (_alignmentInstance != null) _alignmentInstance.gameObject.SetActive(false);

    SetLockTextColor(Color.red);
    SetMinigameTextColor(Color.red);
    UpdateCalibrationSlider(0f); // reset on reset
}


    // small editor helper to manually force a target (not serialized)
    public void ForceTarget(Transform t)
    {
        currentTarget = t;
    }

    // For debugging: draw gizmo to visualize aim tolerance
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, scanRange);
        }
    }
    
    void ApplyAllocationsIfNeeded()
    {
        if (GameManager.Instance == null) return;

        float eng = GameManager.Instance.engineAlloc;
        float sh = GameManager.Instance.shieldsAlloc;
        float sen = GameManager.Instance.sensorsAlloc;
        float def = GameManager.Instance.deflectorsAlloc;
        float aut = GameManager.Instance.automationAlloc;

        if (!Mathf.Approximately(eng, prevEngineAlloc) ||
            !Mathf.Approximately(sh, prevShieldsAlloc) ||
            !Mathf.Approximately(sen, prevSensorsAlloc) ||
            !Mathf.Approximately(def, prevDeflectorsAlloc) ||
            !Mathf.Approximately(aut, prevAutomationAlloc))
        {
            prevEngineAlloc = eng;
            prevShieldsAlloc = sh;
            prevSensorsAlloc = sen;
            prevDeflectorsAlloc = def;
            prevAutomationAlloc = aut;
            ApplyAllocations();
        }
    }

    void ApplyAllocations()
    {
        if (GameManager.Instance == null) return;

        // normalize (expected total 100)
        ef = Mathf.Clamp01(GameManager.Instance.engineAlloc / 100f);
        shf = Mathf.Clamp01(GameManager.Instance.shieldsAlloc / 100f);
        sf = Mathf.Clamp01(GameManager.Instance.sensorsAlloc / 100f);
        df = Mathf.Clamp01(GameManager.Instance.deflectorsAlloc / 100f);
        af = Mathf.Clamp01(GameManager.Instance.automationAlloc / 100f);

        // Sensors: bigger scan range, bigger aim tolerance, faster lock
        scanRange = baseScanRange * (1f + 0.6f * sf); // up to +60% range for full sensors
        aimTolerancePixels = Mathf.Max(12f, baseAimTolerance * (1f + 0.5f * sf)); // up to +50% tolerance
        lockHoldTime = Mathf.Max(0.5f, baseLockHoldTime * (1f - 0.4f * sf + (1f - ef) * 0.15f));
        // sensors make locking faster; very low engine slightly increases lock time as a penalty

        // Engine: affects rotation responsiveness and drift (higher engine = easier alignment)
        autoRotateSpeed = baseAutoRotateSpeed * Mathf.Max(0.4f, 0.6f + 0.8f * ef + 0.15f * sf);
        cameraFollowSpeed = baseCameraFollowSpeed * (0.7f + 0.6f * ef);
        satelliteDriftSpeed = baseSatelliteDriftSpeed * Mathf.Max(4f, (1.3f - 0.75f * ef - 0.15f * af) * baseSatelliteDriftSpeed) / baseSatelliteDriftSpeed;
        // the satelliteDriftSpeed line ensures we keep a positive drift; calculation results in multiplier >= 4 degrees fallback.

        // Automation: better player control (playerAdjustSpeed) and reduces sync duration
        playerAdjustSpeed = basePlayerAdjustSpeed * (0.6f + 1.4f * af);
        syncDuration = Mathf.Max(0.4f, baseSyncDuration * (1f - 0.35f * af - 0.15f * sf + 0.1f * df));

        // Deflectors: widen tolerance (sweet-spot bigger), but small increase in hold to balance
        syncThreshold = Mathf.Max(1f, baseSyncThreshold * (1f + 0.5f * df - 0.35f * af));
        // deflectors increase threshold (easier), automation reduces threshold as it's more precise

        // clamp sanity
        autoRotateSpeed = Mathf.Clamp(autoRotateSpeed, 0.3f, baseAutoRotateSpeed * 3f);
        playerAdjustSpeed = Mathf.Clamp(playerAdjustSpeed, 8f, basePlayerAdjustSpeed * 3f);
        satelliteDriftSpeed = Mathf.Clamp(satelliteDriftSpeed, 6f, baseSatelliteDriftSpeed * 4f);
        syncThreshold = Mathf.Clamp(syncThreshold, 1f, baseSyncThreshold * 3f);
        syncDuration = Mathf.Clamp(syncDuration, 0.3f, baseSyncDuration * 2f);
        lockHoldTime = Mathf.Clamp(lockHoldTime, 0.3f, baseLockHoldTime * 2f);
        aimTolerancePixels = Mathf.Clamp(aimTolerancePixels, 8f, baseAimTolerance * 2f);

        // (optional) debug - remove/comment out in release builds
        // Debug.Log($"Applied allocations: E{ef:F2} S{shf:F2} Se{sf:F2} D{df:F2} A{af:F2}");
    }
}
