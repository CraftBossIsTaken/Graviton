using System.Collections;
using UnityEngine;

public class BlackHoleRetreat : MonoBehaviour
{
    [Header("Tidal Force Settings")]
public float tidalGravityMultiplier = 2.0f; // How much stronger gravity becomes during a wave
private float originalGravityInfluence;

    [Header("References")]
    public Transform ship;
    public static BlackHoleRetreat Instance { get; private set; }

    [Header("Flight Settings")]
    public float baseSpeed = 20f;
    public float maxSpeed = 100f;
    public float powerLevel = 0.5f;
    public float acceleration = 0.5f;
    public float gravityInfluence = 1.5f;
    public float waveThrustReduction = 0.6f;

    [Header("Distance Settings")]
    public float startDistance = 200f;
    public float safeDistance = 1500f;

    [Header("Wave Settings")]
    public float waveIntervalMin = 90f;
    public float waveIntervalMax = 120f;
    public float waveDuration = 5f;

    [Header("Runtime Info (Debug)")]
    public float currentSpeed;
    public float distance;
    public float escapeProgress;

    [Header("Warning Integration")]
    [Tooltip("Audio sources that will be used for black hole approach warnings.")]
    public AudioSource[] warningSources;

    [Tooltip("Audio clip to play when black hole starts approaching.")]
    public AudioClip approachWarningClip;

    private bool waveActive = false;
    private bool isApproaching = false;
    public bool hasWarned = false;
    public bool isPlayingWarning = false;

    private Vector3 direction;
    private float lastDistance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        // Hook into the GravitationalWaveManager's events
        GravitationalWaveManager.OnWaveDetected += HandleWaveDetected;
        GravitationalWaveManager.OnWaveStart += HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd += HandleWaveEnd;
    }

    void OnDisable()
    {
        GravitationalWaveManager.OnWaveDetected -= HandleWaveDetected;
        GravitationalWaveManager.OnWaveStart -= HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd -= HandleWaveEnd;
    }
private float tidalMultiplier = 1f; // 1 normally, >1 during Tidal Forces

private void HandleWaveStart(GravitationalWaveManager.GravitationalWavePreset preset)
{
    if (preset.name == "Tidal Forces" || preset.name == "Tidal")
    {
        tidalMultiplier = tidalGravityMultiplier; // e.g., 2
        Debug.Log("[BlackHoleRetreat] Tidal Forces detected! Gravity temporarily increased.");
    }
}

private void HandleWaveEnd(GravitationalWaveManager.GravitationalWavePreset preset)
{
    // Smoothly reset tidal multiplier
    StartCoroutine(AdjustTidalMultiplier(1f, 2f));
}

private IEnumerator AdjustTidalMultiplier(float target, float duration)
{
    float start = tidalMultiplier;
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime / duration;
        tidalMultiplier = Mathf.Lerp(start, target, t);
        yield return null;
    }
}


    void Start()
    {
        if (!ship)
        {
            Debug.LogError("Ship reference missing!");
            enabled = false;
            return;
        }

        direction = (transform.position - ship.position).normalized;
        lastDistance = Vector3.Distance(transform.position, ship.position);

      
        if (approachWarningClip != null && warningSources != null)
        {
            foreach (var src in warningSources)
            {
             ///   if (src != null)
   //                 src.clip = approachWarningClip;
            }
        }

        StartCoroutine(GravitationalWaveRoutine());
    }

    void Update()
    {
       

        distance = Vector3.Distance(transform.position, ship.position);
        direction = (transform.position - ship.position).normalized;
        escapeProgress = Mathf.InverseLerp(startDistance, safeDistance, distance);

        float distanceFactor = Mathf.Clamp01(distance / safeDistance);
    float gravityPenalty = Mathf.Pow(distanceFactor, gravityInfluence * tidalMultiplier);

        float effectivePower = powerLevel;
        if (waveActive)
            effectivePower *= waveThrustReduction;

        float targetSpeed = baseSpeed * (0.5f + effectivePower) * (1f + distanceFactor * 0.8f);
        
        targetSpeed = Mathf.Clamp(targetSpeed, -maxSpeed, maxSpeed);
float gravityEffect = gravityInfluence * tidalMultiplier * (1f - distanceFactor);
targetSpeed -= gravityEffect * baseSpeed * 1.3f; 


        if (effectivePower < 0.2f)
        {
            float pullStrength = (0.2f - effectivePower) / 0.2f;
            float pullForce = baseSpeed * pullStrength * (gravityInfluence * 1.2f);
            targetSpeed = -pullForce;
        }

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);
        transform.root.position += direction * currentSpeed * Time.deltaTime;

        // Detect if approaching the ship
        bool currentlyApproaching = distance < lastDistance;
        if (currentlyApproaching && !isApproaching)
        {
            TriggerApproachWarning();
        }

        isApproaching = currentlyApproaching;
        lastDistance = distance;
    }

    IEnumerator GravitationalWaveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(waveIntervalMin, waveIntervalMax));
            StartCoroutine(ApplyWaveEffect());
        }
    }

    IEnumerator ApplyWaveEffect()
    {
        waveActive = true;
        yield return new WaitForSeconds(waveDuration);
        waveActive = false;
    }

    public void SetPowerLevel(float newPower)
    {
        powerLevel = Mathf.Clamp01(newPower);
    }

    // -------------------
    // Event Handlers
    // -------------------
    private void HandleWaveDetected(GravitationalWaveManager.GravitationalWavePreset preset, float timeUntilImpact)
    {
        // optional: early warning logic
        Debug.Log($"[BlackHoleRetreat] Wave detected: {preset.name}, {timeUntilImpact:F1}s until impact.");
    }


    // -------------------
    // Warning Logic
    // -------------------
 // -------------------
// Warning Logic (uses GWM queue)
// -------------------
private void TriggerApproachWarning()
{
    if (!hasWarned && !isPlayingWarning)
    {
        hasWarned = true;
        isPlayingWarning = true;

        // Queue the warning announcement through the manager if available
        if (GravitationalWaveManager.Instance != null && approachWarningClip != null)
        {
            string subtitle = "Critical warning. Engine output insufficient to counter gravitational force. Redistribute power.";
            GravitationalWaveManager.Instance.EnqueueAnnouncement(
                approachWarningClip,
                subtitle,
                warningSources
            );
        }
        else
        {
            // fallback if manager not available
            foreach (var src in warningSources)
            {
                if (src == null) continue;
                src.clip = approachWarningClip;
                src.loop = false;
                src.Play();
            }
        }

        // Reset flags after clip duration
        StartCoroutine(ResetWarningAfterDelay(approachWarningClip != null ? approachWarningClip.length : 5f));
    }
}

private IEnumerator ResetWarningAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    isPlayingWarning = false;
    hasWarned = false; // allow retriggering later
}


    private IEnumerator PlayApproachWarning()
    {
        isPlayingWarning = true;

        // Wait for any current playback to finish
        if (warningSources != null && warningSources.Length > 0)
        {
            bool anyPlaying;
            do
            {
                anyPlaying = false;
                foreach (var src in warningSources)
                {
                    if (src != null && src.isPlaying)
                    {
                        anyPlaying = true;
                        break;
                    }
                }
                if (anyPlaying) yield return null;
            }
            while (anyPlaying);
        }
        // Show subtitle while playing approach warning
// Show subtitle while playing approach warning
if (GravitationalWaveManager.Instance != null && approachWarningClip != null)
{
    string subtitle = "Critical warning. Engine output insufficient to counter gravitational force. Redistribute power.";
    float duration = approachWarningClip.length;
    GravitationalWaveManager.Instance.StartCoroutine(
        GravitationalWaveManager.Instance.ShowSubtitleRoutine(subtitle, duration)
    );
}


        // Assign clip & play
        if (approachWarningClip != null && warningSources != null)
        {
            foreach (var src in warningSources)
            {
                if (src != null)
                {
                    src.clip = approachWarningClip;
                    src.loop = false;
                    src.Play();
                }
            }

            yield return new WaitForSeconds(approachWarningClip.length);
        }

        isPlayingWarning = false;
        hasWarned = false; // allow retriggering later if it happens again
    }
        
}
