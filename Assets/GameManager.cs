using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public Interact interact;
    public static GameManager Instance { get; private set; }
    public BlackHoleRetreat sc;
    public float engineAlloc = 50f;
    public float shieldsAlloc;
    public TextMeshPro hI;
    public float sensorsAlloc;
    public float deflectorsAlloc;
    public float automationAlloc;
    public Slider engine;
    public Slider shields;
    public Slider sensors;
    public Slider deflectors;
    public Slider automation;
    public float hullIntegrity;
    public float shieldIntegrity;
    public float distFromHorizon;
    public TextMeshPro distFromHorizonText;
    public TextMeshPro shieldIntegrityText;
    public TextMeshPro backupPowerText;
    public float backupPower;
    public tracker Track;
public ShipBreakManager shipBreakManager;
    private float totalDamageTaken = 0f;
    private float totalBackupUsed = 0f;
    private float totalShieldsBlocked = 0f;
    private int hullHitCount = 0;
    private float timeElapsed = 0f;
    [Header("UI Elements (Stats Canvas)")]
    public TextMeshProUGUI damageTakenText;
    public TextMeshProUGUI timeElapsedText;
    public TextMeshProUGUI backupUsedText;
    public TextMeshProUGUI shieldsBlockedText;
    public TextMeshProUGUI hullHitsText;
    public TextMeshProUGUI runStatsTitleText; // new: "Run Statistics" text
    public Button returnButton; // new: TMP button
    public GameObject deathCanvas;
    // Track previous hull integrity for threshold detection
    private float _prevHullIntegrity;
[Header("Power Consumption Rates (per second)")]
    [Tooltip("Base power drain from the Engine/Thrusters for every 10% of engineAlloc.")]
    public float engineDrainRate = 0.5f;
    [Tooltip("Base power drain for all other sectors per second.")]
    public float sectorDrainRate = 0.05f;
    void UpdateHologramFade()
    {
        TMP3DHologramScanline[] holograms = FindObjectsOfType<TMP3DHologramScanline>();

        foreach (var holo in holograms)
        {
            holo.columnFadeThreshold = 0.5f - (automationAlloc / 100f);
            holo.columns = 2000 - ((int)automationAlloc * 10);
        }
    }

    public void addBackupPower(float powertoAdd)
    {
        backupPower += powertoAdd;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
       
        _prevHullIntegrity = hullIntegrity; // initialize
    }

    public void Update()
{
    timeElapsed += Time.deltaTime;

    sc.powerLevel = engineAlloc / 100f;
    engine.value = engineAlloc;
    shields.value = shieldsAlloc;
    sensors.value = sensorsAlloc;
    deflectors.value = deflectorsAlloc;
    automation.value = automationAlloc;

    backupPowerText.text = "Backup power: " + Mathf.Round(backupPower) + "%";
    hI.text = "Hull Integrity: " + hullIntegrity + "%";
    shieldIntegrityText.text = "Shield Integrity: " + shieldIntegrity + "%";

    float f = Mathf.Round(distFromHorizon * 10.0f) * 0.1f;
    distFromHorizonText.text = "Distance from Event Horizon: " + f + " km";
float totalDrain = 0f;
    totalDrain += engineAlloc / 10f * engineDrainRate; 
    float otherAlloc = shieldsAlloc + sensorsAlloc + deflectorsAlloc + automationAlloc;
    totalDrain += otherAlloc / 100f * sectorDrainRate;

    // Apply the drain
    backupPower -= totalDrain * Time.deltaTime; 
    backupPower = Mathf.Max(0f, backupPower); // Prevent power from going negative

    // --- Power Failure/Game Over Check ---
    if (backupPower <= 0f)
    {
        // Implement your power failure logic here (e.g., set moveSpeed to 0, 
        // disable shields/sensors, play a sound, show UI warning)
        Debug.LogWarning("Backup Power Exhausted! Systems Offline!");
        // movableObject.GetComponent<CameraControls>().moveSpeed = 0f; // Example
    }
    CheckHullIntegrityThresholds();

    // DAMAGE TRACKING
    if (hullIntegrity < _prevHullIntegrity)
    {
        float damage = _prevHullIntegrity - hullIntegrity;
        totalDamageTaken += damage;
        hullHitCount++;

        float shieldAbsorb = Mathf.Min(damage, shieldIntegrity);
        totalShieldsBlocked += shieldAbsorb;
    }

    totalBackupUsed = Mathf.Max(totalBackupUsed, backupPower);

    // Update previous hull AFTER tracking
    _prevHullIntegrity = hullIntegrity;
}


   private bool shipAlreadyBroken = false;

private void CheckHullIntegrityThresholds()
{
    if (GravitationalWaveManager.Instance == null) return;

    // Thresholds: 75%, 50%, 25%, 0%
    float[] thresholds = { 75f, 50f, 25f, 0f };
    int[] clipIndices = { 3, 6, 5, 4 }; // corresponding clip indices in availableClips

    for (int i = 0; i < thresholds.Length; i++)
    {
        if (_prevHullIntegrity > thresholds[i] && hullIntegrity <= thresholds[i])
        {
            // Hull crossed downward past threshold
            AudioClip clip = null;
            string subtitle = "";
            if (clipIndices[i] >= 0 && clipIndices[i] < GravitationalWaveManager.Instance.availableClips.Length)
            {
                clip = GravitationalWaveManager.Instance.availableClips[clipIndices[i]];

                if (GravitationalWaveManager.Instance.availableSubtitles != null &&
                    clipIndices[i] < GravitationalWaveManager.Instance.availableSubtitles.Length &&
                    !string.IsNullOrEmpty(GravitationalWaveManager.Instance.availableSubtitles[clipIndices[i]]))
                {
                    subtitle = GravitationalWaveManager.Instance.availableSubtitles[clipIndices[i]];
                }
                else if (clip != null)
                {
                    subtitle = clip.name;
                }
            }

            // Enqueue the announcement
            GravitationalWaveManager.Instance.EnqueueAnnouncement(clip, subtitle, GravitationalWaveManager.Instance.globalSpeakers);

            // --- NEW: Trigger ship break 3s after hitting 0% ---
            if (thresholds[i] == 0f && !shipAlreadyBroken && shipBreakManager != null)
            {
                shipAlreadyBroken = true;
                StartCoroutine(TriggerShipBreakAfterDelay(3f));
            }
        }
    }
}
private IEnumerator TriggerShipBreakAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    shipBreakManager.BreakShip();
     deathCanvas.SetActive(true);
        StartCoroutine(AnimateStatsCoroutine());
}

  private IEnumerator AnimateStatsCoroutine()
    {
        // Hide all text and button at start
        runStatsTitleText.alpha = 0f;
        damageTakenText.alpha = 0f;
        timeElapsedText.alpha = 0f;
        backupUsedText.alpha = 0f;
        shieldsBlockedText.alpha = 0f;
        hullHitsText.alpha = 0f;
        returnButton.gameObject.SetActive(false);

        // --- 1) Fade in title ---
        yield return StartCoroutine(FadeText(runStatsTitleText, 1f, 0.5f));

        // --- 2) Animate each stat line ---
        string[] lines = new string[]
        {
             $"Time Elapsed: {Mathf.FloorToInt(timeElapsed / 60f):D2}:{Mathf.FloorToInt(timeElapsed % 60f):D2}",
            $"Ship Damage Taken: {Mathf.RoundToInt(totalDamageTaken)}%",
            $"Backup Power Used: {Mathf.RoundToInt(totalBackupUsed)}%",
            $"Shields Blocked: {Mathf.RoundToInt(totalShieldsBlocked)}%",
            $"Hull Hits Taken: {hullHitCount}"
        };

        TextMeshProUGUI[] textComponents = new TextMeshProUGUI[]
        {
             timeElapsedText, damageTakenText, backupUsedText, shieldsBlockedText, hullHitsText
        };

        for (int i = 0; i < lines.Length; i++)
        {
            yield return StartCoroutine(TypeText(textComponents[i], lines[i]));
            yield return new WaitForSeconds(1f); // pause 1s after each line
        }

        // --- 3) Fade in Return button ---
        returnButton.gameObject.SetActive(true);
        CanvasGroup buttonCanvas = returnButton.GetComponent<CanvasGroup>();
        if (buttonCanvas == null)
            buttonCanvas = returnButton.gameObject.AddComponent<CanvasGroup>();

        buttonCanvas.alpha = 0f;
        float t = 0f;
        float duration = 0.5f;
        while (t < duration)
        {
            buttonCanvas.alpha = Mathf.Lerp(0f, 1f, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        buttonCanvas.alpha = 1f;
    }

    private IEnumerator TypeText(TextMeshProUGUI textComp, string fullText)
    {
        textComp.text = "";
        textComp.alpha = 1f;

        for (int i = 0; i < fullText.Length; i++)
        {
            textComp.text += fullText[i];
            yield return new WaitForSeconds(0.1f); // letter delay
        }
    }

    private IEnumerator FadeText(TextMeshProUGUI textComp, float targetAlpha, float duration)
    {
        float startAlpha = textComp.alpha;
        float t = 0f;

        while (t < duration)
        {
            textComp.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            t += Time.deltaTime;
            yield return null;
        }

        textComp.alpha = targetAlpha;
    }
    public bool TryModifyAlloc(string system, float change)
    {
        float newEngine = engineAlloc;
        float newShields = shieldsAlloc;
        float newSensors = sensorsAlloc;
        float newDeflectors = deflectorsAlloc;
        float newAutomation = automationAlloc;

        switch (system)
        {
            case "Engine": newEngine += change; break;
            case "Shields": newShields += change; break;
            case "Sensors": newSensors += change; break;
            case "Deflectors": newDeflectors += change; break;
            case "Automation": newAutomation += change; break;
            default:
                Debug.LogWarning($"Unknown system '{system}'");
                return false;
        }

        newEngine = Mathf.Max(0, newEngine);
        newShields = Mathf.Max(0, newShields);
        newSensors = Mathf.Max(0, newSensors);
        newDeflectors = Mathf.Max(0, newDeflectors);
        newAutomation = Mathf.Max(0, newAutomation);

        UpdateHologramFade();

        float total = newEngine + newShields + newSensors + newDeflectors + newAutomation;
        if (total > 100f)
        {
            float scale = 100f / total;
            newEngine *= scale;
            newShields *= scale;
            newSensors *= scale;
            newDeflectors *= scale;
            newAutomation *= scale;
            Debug.Log("Allocations auto-normalized to total 100");
        }

        engineAlloc = newEngine;
        shieldsAlloc = newShields;
        sensorsAlloc = newSensors;
        deflectorsAlloc = newDeflectors;
        automationAlloc = newAutomation;

        Debug.Log($"[{system}] modified by {change}. New total = {total}/100");
        return true;
    }
    public void DealDamage(int damage)
    {
        hullIntegrity -= damage;
            Debug.Log($"Hull damaged by {damage}, new value: {hullIntegrity}");
        int idx = Random.Range(7, 9); // 7 or 8 inclusive
        GravitationalWaveManager.Instance.EnqueueAnnouncementByIndex(idx);
    }
    public void Start()
    {
        UpdateHologramFade();
    }
        public void ShowEndRunStats()
    {
        deathCanvas.SetActive(true);
        if (damageTakenText != null)
            damageTakenText.text = $"Ship Damage Taken: {Mathf.RoundToInt(totalDamageTaken)}%";

        if (timeElapsedText != null)
        {
            int minutes = Mathf.FloorToInt(timeElapsed / 60f);
            int seconds = Mathf.FloorToInt(timeElapsed % 60f);
            timeElapsedText.text = $"Time Elapsed: {minutes:D2}:{seconds:D2}";
        }

        if (backupUsedText != null)
            backupUsedText.text = $"Backup Power Used: {Mathf.RoundToInt(totalBackupUsed)}%";

        if (shieldsBlockedText != null)
            shieldsBlockedText.text = $"Shields Blocked: {Mathf.RoundToInt(totalShieldsBlocked)}%";

        if (hullHitsText != null)
            hullHitsText.text = $"Hull Hits Taken: {hullHitCount}";
    }
}
