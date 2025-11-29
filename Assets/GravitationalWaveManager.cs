// GravitationalWaveManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class GravitationalWaveManager : MonoBehaviour
{
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
      
    }
    public static GravitationalWaveManager Instance { get; private set; }

    // Events for other systems to subscribe to
    public static event Action<GravitationalWavePreset> OnWaveStart;
    public static event Action<GravitationalWavePreset> OnWaveEnd;
    // Invoked when a wave is detected (pre-warning). float = seconds until impact.
    public static event Action<GravitationalWavePreset, float> OnWaveDetected;

    [Header("Audio fallback")]
    [Tooltip("Audio clips played when sensors allocation is too low. One will be chosen randomly.")]
    public AudioClip[] lowSensorFallbackClips;

    [Tooltip("Subtitles for each fallback clip (same length as lowSensorFallbackClips).")]
    public string[] lowSensorFallbackSubtitles;

    [Serializable]
    public class GravitationalWavePreset
    {
        public string name = "Wave";
        [Tooltip("Duration of the active wave (seconds)")]
        public float duration = 8f;

        [Header("Mechanical effects (tweak to your implementation)")]
        public float gravityMultiplier = 1f;      // multiplies black hole pull
        public float sensorMultiplier = 1f;       // multiplies sensor effectiveness
        public float backupPowerDrainPerSec = 0f; // drains backup
        public float asteroidImpulse = 0f;        // magnitude of impulse applied to orbiting objects

        [Header("Audio (choose index from global list or override speakers)")]
        [Tooltip("Index into manager.availableClips. -1 = no audio")]
        public int clipIndex = -1;

        [Tooltip("If true, use manager.globalSpeakers; otherwise use presetSpeakers")]
        public bool useGlobalSpeakers = true;

        [Tooltip("Optional per-preset list of AudioSources to broadcast the chosen clip to")]
        public AudioSource[] presetSpeakers;
    }

    [Header("Wave presets")]
    public List<GravitationalWavePreset> presets = new List<GravitationalWavePreset>();

    [Header("Global audio assets & speaker mapping")]
    [Tooltip("A list of AudioClips you can reference from presets by index.")]
    public AudioClip[] availableClips;
    [Tooltip("A matching array of subtitles (same indexing as availableClips). If empty or entry is blank the clip name will be used.")]
    public string[] availableSubtitles;
    [Tooltip("Fallback subtitle when sensors are too low to classify the incoming event.")]
    [TextArea] public string lowSensorFallbackSubtitle = "Incoming gravitational anomaly detected — sensors insufficient to classify.";
    [Tooltip("Default speakers used when preset.useGlobalSpeakers==true")]
    public AudioSource[] globalSpeakers;

    [Header("Timing")]
    public float intervalBetweenWaves = 120f; // time between waves
    public float preWarningTime = 10f;        // time from detection -> wave start
    [Tooltip("Minimum blinking duration (so very short preWarningTime still blinks for a sensible time)")]
    public float minBlinkDuration = 5f;

    [Header("Warning lights / blink")]
    [Tooltip("Objects to toggle on/off as warning lights (setActive true/false).")]
    public GameObject[] warningLights;
    [Tooltip("Seconds between toggles (0.25 = 4 flashes per second)")]
    public float blinkInterval = 0.25f;

    [Header("Sensors dependency")]
    [Tooltip("If sensorsAlloc is below this value the AI will not classify the wave and a generic subtitle will be used.")]
    public float sensorThresholdForClassification = 20f;

    [Header("Subtitles (UI)")]
    public TextMeshProUGUI subtitleText; // assign a TMP UI element
    [Tooltip("How long subtitles remain visible if clip length is unknown")]
    public float defaultSubtitleDuration = 4f;
    [Tooltip("Whether subtitles should be shown during pre-warning")]
    public bool showSubtitleDuringPreWarning = true;

    [Header("Black hole hook (optional)")]
    [Tooltip("Assign your BlackHoleRetreat / controller object here if you have one. The manager will try to invoke SetTemporaryGravityMultiplier(multiplier, duration) via reflection if present.")]
    public MonoBehaviour blackHoleController; // optional: assign your BlackHoleRetreat instance here

    [Header("Intro sequence (optional)")]
    [Tooltip("If you want a default configured intro sequence, put indices here (indexes into availableClips).")]
    public int[] introClipIndices;
    [Tooltip("Gap between intro clips when using PlayConfiguredIntro")]
    public float introGapBetween = 0.25f;

    [Header("Misc")]
    public bool randomizePreset = true;

    // internal
    bool _running = false;
    Coroutine _loopCoroutine;

    // ---------------------------
    // Announcement queue system
    // ---------------------------
    enum AnnouncementMode { OneShot, LoopForDuration }

    class AnnouncementItem
    {
        public AudioClip clip;
        public string subtitle;
        public AudioSource[] targets;
        public float duration; // used if mode == LoopForDuration (total time announcement occupies)
        public AnnouncementMode mode;
    }

    private Queue<AnnouncementItem> _announcementQueue = new Queue<AnnouncementItem>();
    private Coroutine _announcementPlayerCoroutine;
    private object _announcementLock = new object();

    // ---------------------------
    // Subtitle queue system
    // ---------------------------
    class SubtitleItem { public string text; public float duration; public SubtitleItem(string t, float d) { text = t; duration = d; } }
    private Queue<SubtitleItem> _subtitleQueue = new Queue<SubtitleItem>();
    private Coroutine _subtitleProcessor;

    // ---------------------------
    // Light overlap / mode manager
    // ---------------------------
    // We'll support requests for flash (warning) and hold (critical). Hold wins when both are active.
    int _flashingRequests = 0;
    int _holdRequests = 0;
    Coroutine _flashingCoroutine;

    void Start()
    {
        if (presets == null || presets.Count == 0)
            PopulateDefaultPresets();

        // Start intro sequence first (before main loop)
        StartCoroutine(IntroThenStartLoop());
    }

    IEnumerator IntroThenStartLoop()
    {
        // If intro indices are not set, try to fallback to showing the first available clip's subtitle only.
        int[] clipsToPlay = introClipIndices;
        if ((clipsToPlay == null || clipsToPlay.Length == 0) && availableClips != null && availableClips.Length > 0)
        {
            clipsToPlay = new int[] { 0 };
        }

        if (clipsToPlay != null && clipsToPlay.Length > 0)
        {
            // Play intro sequence (this will enqueue audio/subtitles).
            yield return StartCoroutine(PlayIntroSequenceCoroutine(clipsToPlay, globalSpeakers, introGapBetween));
        }
        else
        {
            Debug.Log("[GWM] No introClipIndices and no availableClips — skipping intro.");
        }

        // Now start main wave loop
        _running = true;
        _loopCoroutine = StartCoroutine(WaveLoop());
    }

    void OnDestroy()
    {
        _running = false;
        if (_loopCoroutine != null)
            StopCoroutine(_loopCoroutine);

        if (_announcementPlayerCoroutine != null)
            StopCoroutine(_announcementPlayerCoroutine);

        if (_subtitleProcessor != null)
            StopCoroutine(_subtitleProcessor);

        if (_flashingCoroutine != null)
            StopCoroutine(_flashingCoroutine);
    }

    void PopulateDefaultPresets()
    {
        presets = new List<GravitationalWavePreset>() {
            new GravitationalWavePreset { name="Tidal", duration=8f, gravityMultiplier=1.5f, asteroidImpulse=0.5f, clipIndex=0 },
            new GravitationalWavePreset { name="Interference", duration=10f, sensorMultiplier=0.6f, clipIndex=1 },
            new GravitationalWavePreset { name="Drain", duration=7f, backupPowerDrainPerSec=4f, clipIndex=2 },
            new GravitationalWavePreset { name="Ripple", duration=6f, asteroidImpulse=2f, clipIndex=3 }
        };
    }

    IEnumerator WaveLoop()
    {
        while (_running)
        {
            // Wait until the next pre-warning window
            float wait = Mathf.Max(0.1f, intervalBetweenWaves - preWarningTime);
            yield return new WaitForSeconds(wait);

            // Choose a preset
            GravitationalWavePreset preset = ChoosePreset();
            if (preset == null) continue;

            // Pre-warning sequence (telegraph)
            float warnDuration = Mathf.Max(preWarningTime, minBlinkDuration);
            // Broadcast detection
            OnWaveDetected?.Invoke(preset, warnDuration);

            // Start blinking lights and audio for the pre-warning period
            if (warningLights != null && warningLights.Length > 0)
                StartWarningBlink(warnDuration);

            Coroutine audioRoutine = StartCoroutine(WarningAudioRoutine(preset, warnDuration));

            // Wait the pre-warning duration
            yield return new WaitForSeconds(warnDuration);

            // Ensure warning blink stop
            StopWarningBlink();

            // Start the wave
            OnWaveStart?.Invoke(preset);

            // Apply continuous wave effects for duration
            yield return StartCoroutine(ApplyWave(preset));

            OnWaveEnd?.Invoke(preset);

            // small cooldown implicit as loop repeats
        }
    }

    GravitationalWavePreset ChoosePreset()
    {
        if (presets == null || presets.Count == 0) return null;
        if (!randomizePreset) return presets[0];
        return presets[UnityEngine.Random.Range(0, presets.Count)];
    }

    // ---------------------------
    // Light control (overlap-safe)
    // ---------------------------
    /// <summary>
    /// Request a flashing (warning) state for the given duration. Multiple callers can request; it will stop only when all requesters have released or duration elapsed.
    /// </summary>
    public void StartWarningBlink(float duration)
    {
        StartCoroutine(WarningBlinkRequester(duration));
    }

    IEnumerator WarningBlinkRequester(float duration)
    {
        _flashingRequests++;
        // start flashing coroutine if not running
        EnsureFlashingCoroutine();
        yield return new WaitForSeconds(duration);
        _flashingRequests = Math.Max(0, _flashingRequests - 1);
        EvaluateLightsStateImmediate();
    }

    /// <summary>
    /// Stop flashing requests immediately (force release).
    /// </summary>
    public void StopWarningBlink()
    {
        _flashingRequests = 0;
        EvaluateLightsStateImmediate();
    }

    /// <summary>
    /// Request a hold-red (critical) state for the given duration. Hold takes precedence over flashing.
    /// </summary>
    public void StartCriticalHold(float duration)
    {
        StartCoroutine(CriticalHoldRequester(duration));
    }

    IEnumerator CriticalHoldRequester(float duration)
    {
        _holdRequests++;
        EvaluateLightsStateImmediate();
        yield return new WaitForSeconds(duration);
        _holdRequests = Math.Max(0, _holdRequests - 1);
        EvaluateLightsStateImmediate();
    }

    void EnsureFlashingCoroutine()
    {
        if (_flashingCoroutine == null)
        {
            _flashingCoroutine = StartCoroutine(FlashingCoroutine());
        }
    }

    void StopFlashingCoroutine()
    {
        if (_flashingCoroutine != null)
        {
            StopCoroutine(_flashingCoroutine);
            _flashingCoroutine = null;
        }
    }

    IEnumerator FlashingCoroutine()
    {
        bool state = true;
        // Ensure initial visible state for the flash
        SetWarningLightsActive(true, holdRed: false);

        while (_flashingRequests > 0)
        {
            // If a hold request exists, maintain hold-red until it clears (hold wins)
            if (_holdRequests > 0)
            {
                SetWarningLightsActive(true, holdRed: true);
                // wait while hold exists (and while flashing still requested)
                while (_holdRequests > 0 && _flashingRequests > 0)
                {
                    yield return null;
                }
                // reset toggle state so flashing restarts visibly
                state = true;
                continue;
            }

            // No hold => perform flash toggle behavior while flashingRequests > 0
            state = !state;
            SetWarningLightsActive(state, holdRed: false);
            yield return new WaitForSeconds(blinkInterval);
        }

        // When flashing done, ensure lights off (unless hold present)
        if (_holdRequests <= 0)
            SetWarningLightsActive(false, holdRed: false);

        _flashingCoroutine = null;
    }

    /// <summary>
    /// Immediately set lights based on current requests (used e.g. after a hold request changes).
    /// </summary>
    void EvaluateLightsStateImmediate()
    {
        if (_holdRequests > 0)
        {
            // Hold red on
            SetWarningLightsActive(true, holdRed: true);
            StopFlashingCoroutine();
        }
        else if (_flashingRequests > 0)
        {
            // start flashing if not running
            EnsureFlashingCoroutine();
        }
        else
        {
            SetWarningLightsActive(false, holdRed: false);
            StopFlashingCoroutine();
        }
    }
public void EnqueueAnnouncementByIndex(int index)
{
    if (availableClips == null) return;
    if (index < 0 || index >= availableClips.Length) return;

    AudioClip clip = availableClips[index];
    string subtitle = (availableSubtitles != null && index < availableSubtitles.Length) ? availableSubtitles[index] : clip.name;

    EnqueueAnnouncement(clip, subtitle, globalSpeakers);
}


    /// <summary>
    /// Sets the GameObject active flags and tries to color them red if holdRed==true and a Renderer or Light is present.
    /// </summary>
    void SetWarningLightsActive(bool active, bool holdRed)
    {
        if (warningLights == null) return;
        for (int i = 0; i < warningLights.Length; i++)
        {
            var go = warningLights[i];
            if (go == null) continue;
            // toggle GameObject active as previous behavior
            go.SetActive(active);

            // if we need to enforce a red color (attempt best-effort)
            if (active && holdRed)
            {
                // Try Light component
                Light l = go.GetComponent<Light>();
                if (l != null)
                {
                    l.color = Color.red;
                    continue;
                }

                // Try Renderer (mesh/quad)
                Renderer r = go.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    try
                    {
                        r.material.color = Color.red;
                    }
                    catch { /* material may be shared; best-effort only */ }
                }
            }
            // If not holdRed, we won't force a color change here
        }
    }

    // ---------------------------
    // Announcement queue usage
    // ---------------------------
    /// <summary>
    /// Enqueue a one-shot announcement (plays once).
    /// </summary>
   public void EnqueueAnnouncement(AudioClip clip, string subtitle, AudioSource[] targets, bool allowSensorFallback = false)

    {
        if (clip == null && string.IsNullOrEmpty(subtitle)) return;

        var item = new AnnouncementItem()
        {
            clip = clip,
            subtitle = subtitle,
            targets = targets,
            duration = clip != null ? clip.length : defaultSubtitleDuration,
            mode = AnnouncementMode.OneShot
        };
        EnqueueItem(item);
    }

    /// <summary>
    /// Enqueue a looping announcement that occupies the queue for `duration` seconds (useful for pre-warning tones).
    /// </summary>
   public void EnqueueLoopingAnnouncement(AudioClip clip, string subtitle, AudioSource[] targets, float duration, bool allowSensorFallback = false)
    {
        if (clip == null && string.IsNullOrEmpty(subtitle)) return;

        var item = new AnnouncementItem()
        {
            clip = clip,
            subtitle = subtitle,
            targets = targets,
            duration = Mathf.Max(0.01f, duration),
            mode = AnnouncementMode.LoopForDuration
        };
        EnqueueItem(item);
    }

    void EnqueueItem(AnnouncementItem item)
    {
        lock (_announcementLock)
        {
            _announcementQueue.Enqueue(item);
            if (_announcementPlayerCoroutine == null)
            {
                _announcementPlayerCoroutine = StartCoroutine(AnnouncementPlayerLoop());
            }
        }
    }

    IEnumerator AnnouncementPlayerLoop()
    {
        while (true)
        {
            AnnouncementItem next = null;
            lock (_announcementLock)
            {
                if (_announcementQueue.Count > 0)
                    next = _announcementQueue.Dequeue();
                else
                    next = null;
            }

            if (next == null) break;

            // Queue subtitle so subtitles never interrupt each other
            if (!string.IsNullOrEmpty(next.subtitle) && subtitleText != null)
            {
                EnqueueSubtitle(next.subtitle, next.duration);
            }

            // If no audio, just wait duration (subtitles will show via subtitle queue)
            if (next.clip == null || next.targets == null || next.targets.Length == 0)
            {
                yield return new WaitForSeconds(next.duration);
                continue;
            }

            if (next.mode == AnnouncementMode.OneShot)
            {
                // Play once on all targets and wait clip length
                foreach (var s in next.targets)
                {
                    if (s == null) continue;
                    s.clip = next.clip;
                    s.loop = false;
                    s.Play();
                }
                yield return new WaitForSeconds(next.clip.length);
                // cleanup
                foreach (var s in next.targets)
                {
                    if (s == null) continue;
                    if (s.isPlaying) s.Stop();
                    s.clip = null;
                }
            }
            else // LoopForDuration
            {
                float elapsed = 0f;
                // We'll loop the clip for next.duration seconds
                while (elapsed < next.duration)
                {
                    foreach (var s in next.targets)
                    {
                        if (s == null) continue;
                        s.clip = next.clip;
                        s.loop = false;
                        s.Play();
                    }
                    float wait = next.clip != null ? next.clip.length : next.duration - elapsed;
                    yield return new WaitForSeconds(wait);
                    elapsed += wait;
                    foreach (var s in next.targets)
                    {
                        if (s == null) continue;
                        if (s.isPlaying) s.Stop();
                        s.clip = null;
                    }
                }
            }
        }

        // no more items => clear coroutine ref
        _announcementPlayerCoroutine = null;
    }

    // ---------------------------
    // Subtitle queue implementation
    // ---------------------------
    void EnqueueSubtitle(string text, float duration)
    {
        if (string.IsNullOrEmpty(text) || subtitleText == null) return;
        _subtitleQueue.Enqueue(new SubtitleItem(text, Mathf.Max(0.01f, duration)));
        if (_subtitleProcessor == null)
            _subtitleProcessor = StartCoroutine(SubtitleQueueProcessor());
    }

    IEnumerator SubtitleQueueProcessor()
    {
        while (_subtitleQueue.Count > 0)
        {
            var item = _subtitleQueue.Dequeue();
            yield return StartCoroutine(ShowSubtitleRoutine(item.text, item.duration));
        }
        _subtitleProcessor = null;
    }

    public IEnumerator ShowSubtitleRoutine(string text, float duration)
    {
        subtitleText.text = text;
        subtitleText.alpha = 0f;
        subtitleText.gameObject.SetActive(true);

        float fadeIn = 0.6f;
        float fadeOut = 0.6f;
        float visibleTime = Mathf.Max(0, duration - fadeIn - fadeOut);

        // Fade in
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            subtitleText.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
            yield return null;
        }

        // Stay visible
        yield return new WaitForSeconds(visibleTime);

        // Fade out
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            subtitleText.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
            yield return null;
        }

        subtitleText.text = "";
        subtitleText.gameObject.SetActive(false);
    }

    // ---------------------------
    // Pre-warning / warning audio (now using queue)
    // ---------------------------
    IEnumerator WarningAudioRoutine(GravitationalWavePreset preset, float duration)
    {
        if (preset == null) yield break;

        bool sensorsLow = GameManager.Instance != null && GameManager.Instance.sensorsAlloc < sensorThresholdForClassification;

        AudioClip clip = null;
        string subtitleToShow = null;
        AudioSource[] targets = preset.useGlobalSpeakers ? globalSpeakers : preset.presetSpeakers;

        if (sensorsLow)
        {
            if (lowSensorFallbackClips != null && lowSensorFallbackClips.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, lowSensorFallbackClips.Length);
                clip = lowSensorFallbackClips[idx];

                // get matching subtitle if it exists
                if (lowSensorFallbackSubtitles != null && idx < lowSensorFallbackSubtitles.Length && !string.IsNullOrEmpty(lowSensorFallbackSubtitles[idx]))
                    subtitleToShow = lowSensorFallbackSubtitles[idx];
                else
                    subtitleToShow = lowSensorFallbackSubtitle;
            }
            else
            {
                clip = null;
                subtitleToShow = lowSensorFallbackSubtitle;
            }
        }
        else
        {
            clip = GetClipForPreset(preset);
            if (clip != null)
            {
                int idx = preset.clipIndex;
                if (availableSubtitles != null && idx >= 0 && idx < availableSubtitles.Length && !string.IsNullOrEmpty(availableSubtitles[idx]))
                    subtitleToShow = availableSubtitles[idx];
                else
                    subtitleToShow = clip.name;
            }
            else
            {
                subtitleToShow = $"{preset.name} detected.";
            }
        }

        // If no clip or no targets, just show subtitle (if set) and wait
        if ((clip == null || targets == null || targets.Length == 0) && !string.IsNullOrEmpty(subtitleToShow))
        {
            if (showSubtitleDuringPreWarning && subtitleText != null)
            {
                EnqueueSubtitle(subtitleToShow, Mathf.Min(duration, (clip != null ? clip.length : defaultSubtitleDuration)));
            }
            yield return new WaitForSeconds(duration);
            yield break;
        }

        // Enqueue a looping announcement that occupies queue for duration
        EnqueueLoopingAnnouncement(clip, showSubtitleDuringPreWarning ? subtitleToShow : "", targets, duration, allowSensorFallback: true);


        // Keep coroutine alive to mirror original behavior (so callers can wait)
        yield return new WaitForSeconds(duration);
    }

    AudioClip GetClipForPreset(GravitationalWavePreset preset)
    {
        if (preset == null) return null;
        int idx = preset.clipIndex;
        if (idx < 0) return null;
        if (availableClips == null) return null;
        if (idx >= 0 && idx < availableClips.Length) return availableClips[idx];
        return null;
    }

    IEnumerator ApplyWave(GravitationalWavePreset preset)
    {
        if (preset == null) yield break;

        float elapsed = 0f;

        // If you want to apply instant impulses to orbiting objects at start:
        if (preset.asteroidImpulse > 0f)
        {
            // Broadcast global impulse event once (subscribe in OrbitalObject scripts)
            // NOTE: OrbitalObject must provide a public static TriggerGlobalImpulse(float) method.
            try
            {
                OrbitalObject.TriggerGlobalImpulse(preset.asteroidImpulse);
            }
            catch (Exception)
            {
                Debug.LogWarning("[GWM] OrbitalObject.TriggerGlobalImpulse not found. Make sure OrbitalObject exposes this helper.");
            }
        }

        // Try to inform black hole controller (if assigned) to temporarily adjust gravity.
        // We look for a method with signature SetTemporaryGravityMultiplier(float multiplier, float duration)
        if (blackHoleController != null)
        {
            MethodInfo mi = blackHoleController.GetType().GetMethod("SetTemporaryGravityMultiplier",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(float), typeof(float) },
                null);

            if (mi != null)
            {
                try
                {
                    mi.Invoke(blackHoleController, new object[] { preset.gravityMultiplier, preset.duration });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GWM] Failed to invoke SetTemporaryGravityMultiplier on blackHoleController: {ex.Message}");
                }
            }
            else
            {
                // no two-arg method, try single-arg (multiplier only). If it exists we invoke it and rely on that component to handle reset.
                MethodInfo miSingle = blackHoleController.GetType().GetMethod("SetTemporaryGravityMultiplier",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(float) },
                    null);
                if (miSingle != null)
                {
                    try
                    {
                        miSingle.Invoke(blackHoleController, new object[] { preset.gravityMultiplier });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GWM] Failed to invoke SetTemporaryGravityMultiplier(multiplier) on blackHoleController: {ex.Message}");
                    }
                }
            }
        }

        // Continuously apply drains and any per-frame effects for the wave duration
        while (elapsed < preset.duration)
        {
            // Drain backup power
            if (preset.backupPowerDrainPerSec != 0f && GameManager.Instance != null)
                GameManager.Instance.addBackupPower(-preset.backupPowerDrainPerSec * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ---------------------------
    // Intro sequence player (uses announcement queue and subtitle queue)
    // ---------------------------
    /// <summary>
    /// Play a sequence of availableClips by index, in order, through the given speakers (or global if null).
    /// Uses the announcement queue so clips won't cut each other off.
    /// </summary>
    public void PlayIntroSequenceIndices(int[] clipIndices, AudioSource[] targets = null, float gapBetween = 0.25f)
    {
        if (clipIndices == null || clipIndices.Length == 0) return;
        if (targets == null || targets.Length == 0) targets = globalSpeakers;
        StartCoroutine(PlayIntroSequenceCoroutine(clipIndices, targets, gapBetween));
    }

    IEnumerator PlayIntroSequenceCoroutine(int[] clipIndices, AudioSource[] targets, float gapBetween)
    {
        // If no targets provided, we'll still display subtitles, so don't bail out completely.
        if ((targets == null || targets.Length == 0) && availableClips == null)
        {
            Debug.LogWarning("[GWM] No audio targets and no availableClips for intro.");
            yield break;
        }

        for (int i = 0; i < clipIndices.Length; i++)
        {
            int idx = clipIndices[i];
            if (availableClips == null || idx < 0 || idx >= availableClips.Length)
            {
                // If missing clip, optionally still show fallback subtitle for a short moment
                yield return new WaitForSeconds(gapBetween);
                continue;
            }
            AudioClip clip = availableClips[idx];

            // set subtitles for the clip (respect sensor limitation)
            string subtitle = clip != null ? (availableSubtitles != null && idx < availableSubtitles.Length ? availableSubtitles[idx] : clip.name) : "";
     

            // If we have audio targets, enqueue announcement; otherwise just enqueue subtitle
            if (targets != null && targets.Length > 0)
            {
                EnqueueAnnouncement(clip, subtitle, targets);
            }
            else
            {
                EnqueueSubtitle(subtitle, clip != null ? clip.length : defaultSubtitleDuration);
            }

            // Wait for the clip to be played from the queue before continuing (roughly)
            float wait = (clip != null ? clip.length : defaultSubtitleDuration) + gapBetween;
            yield return new WaitForSeconds(wait);
        }
    }

    /// <summary>
    /// Convenience: play the configured introClipIndices via the announcement queue.
    /// </summary>
    public void PlayConfiguredIntro()
    {
        if (introClipIndices == null || introClipIndices.Length == 0) return;
        PlayIntroSequenceIndices(introClipIndices, globalSpeakers, introGapBetween);
    }

    // ---------------------------
    // Debug helper
    // ---------------------------
    [ContextMenu("Trigger Random Preset Immediately")]
    public void TriggerImmediate()
    {
        if (presets == null || presets.Count == 0) return;
        StartCoroutine(TriggerImmediateRoutine(ChoosePreset()));
    }

    IEnumerator TriggerImmediateRoutine(GravitationalWavePreset preset)
    {
        OnWaveDetected?.Invoke(preset, Mathf.Max(preWarningTime, minBlinkDuration));
        yield return new WaitForSeconds(Mathf.Max(preWarningTime, minBlinkDuration));
        OnWaveStart?.Invoke(preset);
        yield return StartCoroutine(ApplyWave(preset));
        OnWaveEnd?.Invoke(preset);
    }
}
