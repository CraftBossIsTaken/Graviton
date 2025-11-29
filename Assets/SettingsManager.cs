using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Audio;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

[Serializable]
public class RebindEntry
{
    [Tooltip("Name of the action in the PlayerInput action asset (case sensitive)")]
    public string actionName;

    [Tooltip("Index of the binding to rebind (0 = first binding). For composites use the part index (eg '1' for 'Positive' part)")]
    public int bindingIndex = 0;

    [Tooltip("Button that will start the rebinding when clicked")]
    public Button rebindButton;

    [Tooltip("Text element that will show the current binding display string")]
    public TMP_Text displayText;
}

/// <summary>
/// SettingsManager
/// - Singleton + DontDestroyOnLoad (will auto-create if missing)
/// - Saves settings to PlayerPrefs and applies them immediately
/// - Supports runtime rebinding of the new Input System
/// - Adds standalone framerate setting
/// </summary>
public class SettingsManager : MonoBehaviour
{
    #region Singleton / Auto-create
    public static SettingsManager instance { get; private set; }

    // If true, a headless SettingsManager will be created if none exists when game starts.
    [Tooltip("If true, SettingsManager will auto-create a headless instance if none exists when a scene loads.")]
    public bool autoCreateIfMissing = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExistsOnLoad()
    {
        if (instance == null)
        {
            if (!Application.isPlaying) return;
            // Try to find any existing in scene
            var existing = FindObjectOfType<SettingsManager>();
            if (existing != null) return;

            // Optionally create a headless one
            var go = new GameObject("SettingsManager_AutoCreated");
            var mgr = go.AddComponent<SettingsManager>();
            // leave UI refs null — still useful to apply saved playerprefs
            mgr.autoCreateIfMissing = true;
        }
    }

    void AwakeSingleton()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Duplicate — destroy this
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    [Header("References")]
    public PlayerInput playerInput; // required for rebinds
    public AudioMixer audioMixer; // expose a master volume parameter (string below)

    [Header("Graphics & Screen")]
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdownTMP;
    public Dropdown resolutionDropdown; // fallback if not using TMP
    public Toggle vSyncToggle;

    [Header("Audio")]
    [Tooltip("Name of the exposed parameter in your AudioMixer (usually 'MasterVolume')")]
    public string masterVolumeParam = "MasterVolume";
    public Slider masterVolumeSlider;

    [Header("Controls")]
    public Slider mouseSensitivitySlider;
    public Toggle invertYToggle;

    [Header("Gameplay / Misc")]
    public Toggle disableShakeToggle;

    [Header("Keybindings (new Input System)")]
    public List<RebindEntry> rebindEntries = new List<RebindEntry>();
    public Button resetBindingsButton;

    [Header("Framerate")]
    public TMP_Dropdown framerateDropdownTMP;
    public Dropdown framerateDropdown; // fallback
    // Framerate options: mapping indices to values (0 = unlimited)
    int[] framerateOptionValues = new int[] { 30, 60, 120, 0 };
    string[] framerateOptionLabels = new string[] { "30", "60", "120", "Unlimited" };

    // Keys for PlayerPrefs
    const string PREF_FULLSCREEN = "settings_fullscreen";
    const string PREF_RESOLUTION_INDEX = "settings_resolution_index";
    const string PREF_VSYNC = "settings_vsync";
    const string PREF_MASTER_VOLUME = "settings_master_vol"; // stored linear 0..1
    const string PREF_MOUSE_SENS = "settings_mouse_sens";
    const string PREF_INVERT_Y = "settings_invert_y";
    const string PREF_DISABLE_SHAKE = "settings_disable_shake";
    const string PREF_REBINDS = "settings_rebinds_json";
    const string PREF_FRAMERATE = "settings_framerate";

    // Resolutions list
    Resolution[] availableResolutions;

    // Active rebind operation (so we can cancel/dispose)
    InputActionRebindingExtensions.RebindingOperation activeRebindOp;

    // Event others can listen to for shake toggle changes
    public static event Action<bool> OnShakeToggled;

    void Awake()
    {
        AwakeSingleton();
        // if duplicate destroyed, stop initialization
        if (instance != this) return;

        // Populate resolution list
        availableResolutions = Screen.resolutions;

        // Populate dropdown UI
        PopulateResolutionDropdown();

        // Populate framerate dropdown UI
        PopulateFramerateDropdown();

        // Wire listeners
        WireUI();

        // Load and apply saved settings
        LoadAllSettings();

        // Refresh rebind displayed strings
        RefreshAllBindingDisplays();
    }

    void OnDestroy()
    {
        if (activeRebindOp != null)
        {
            activeRebindOp.Cancel();
            activeRebindOp.Dispose();
            activeRebindOp = null;
        }
    }

    #region UI Wiring
    void WireUI()
    {
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        if (disableShakeToggle != null) disableShakeToggle.onValueChanged.AddListener(OnDisableShakeChanged);

        if (resolutionDropdownTMP != null) resolutionDropdownTMP.onValueChanged.AddListener(OnResolutionChangedTMP);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (framerateDropdownTMP != null) framerateDropdownTMP.onValueChanged.AddListener(OnFramerateChangedTMP);
        if (framerateDropdown != null) framerateDropdown.onValueChanged.AddListener(OnFramerateChanged);

        if (resetBindingsButton != null) resetBindingsButton.onClick.AddListener(ResetAllBindingsToDefault);

        // Wire rebind buttons
        foreach (var entry in rebindEntries)
        {
            if (entry.rebindButton != null)
            {
                var localEntry = entry; // capture
                entry.rebindButton.onClick.AddListener(() => StartRebind(localEntry));
            }
        }
    }
    #endregion

    #region Resolution / Fullscreen / VSync
    void PopulateResolutionDropdown()
    {
        if (availableResolutions == null || availableResolutions.Length == 0) return;

        List<string> options = new List<string>();
        int currentIndex = 0;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            var r = availableResolutions[i];
            string s = r.width + " x " + r.height + " @ " + r.refreshRate + "Hz";
            options.Add(s);

            if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height && r.refreshRate == Screen.currentResolution.refreshRate)
                currentIndex = i;
        }

        if (resolutionDropdownTMP != null)
        {
            resolutionDropdownTMP.ClearOptions();
            resolutionDropdownTMP.AddOptions(options);
            resolutionDropdownTMP.value = PlayerPrefs.GetInt(PREF_RESOLUTION_INDEX, currentIndex);
            resolutionDropdownTMP.RefreshShownValue();
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = PlayerPrefs.GetInt(PREF_RESOLUTION_INDEX, currentIndex);
            resolutionDropdown.RefreshShownValue();
        }
    }

    public void OnResolutionChangedTMP(int index)
    {
        ApplyResolution(index);
    }
    public void OnResolutionChanged(int index)
    {
        ApplyResolution(index);
    }

    void ApplyResolution(int index)
    {
        if (availableResolutions == null || index < 0 || index >= availableResolutions.Length) return;
        var r = availableResolutions[index];

        // Keep fullscreen state: if the player currently has fullscreen on, keep fullscreen when changing resolution.
        bool isFullscreen = Screen.fullScreen;

        // Use FullScreenWindow to keep a fullscreen appearance while changing resolution (works well cross-platform for downscaling)
        Screen.SetResolution(r.width, r.height, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed, r.refreshRate);

        PlayerPrefs.SetInt(PREF_RESOLUTION_INDEX, index);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        // Change fullscreen mode while keeping current resolution
        // Use FullScreenWindow for consistent fullscreen "keeps fullscreen" behaviour
        if (isFullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnVSyncChanged(bool vsyncOn)
    {
        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;
        PlayerPrefs.SetInt(PREF_VSYNC, vsyncOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    #endregion

    #region Framerate
    void PopulateFramerateDropdown()
    {
        List<string> options = new List<string>(framerateOptionLabels);

        if (framerateDropdownTMP != null)
        {
            framerateDropdownTMP.ClearOptions();
            framerateDropdownTMP.AddOptions(options);
            // set initial value from PlayerPrefs (value stored is actual framerate int)
            int stored = PlayerPrefs.GetInt(PREF_FRAMERATE, 60);
            int idx = Array.IndexOf(framerateOptionValues, stored);
            if (idx < 0) idx = Array.IndexOf(framerateOptionValues, 60); // fallback to 60
            framerateDropdownTMP.value = idx;
            framerateDropdownTMP.RefreshShownValue();
        }

        if (framerateDropdown != null)
        {
            framerateDropdown.ClearOptions();
            framerateDropdown.AddOptions(options);
            int stored = PlayerPrefs.GetInt(PREF_FRAMERATE, 60);
            int idx = Array.IndexOf(framerateOptionValues, stored);
            if (idx < 0) idx = Array.IndexOf(framerateOptionValues, 60);
            framerateDropdown.value = idx;
            framerateDropdown.RefreshShownValue();
        }
    }

    public void OnFramerateChangedTMP(int index)
    {
        ApplyFramerateByIndex(index);
    }
    public void OnFramerateChanged(int index)
    {
        ApplyFramerateByIndex(index);
    }

    void ApplyFramerateByIndex(int index)
    {
        if (index < 0 || index >= framerateOptionValues.Length) return;
        int value = framerateOptionValues[index];
        // 0 = unlimited (we map to -1)
        Application.targetFrameRate = (value == 0) ? -1 : value;
        PlayerPrefs.SetInt(PREF_FRAMERATE, value);
        PlayerPrefs.Save();
    }
    #endregion

    #region Audio
    public void OnMasterVolumeChanged(float linear01)
    {
        // slider expected 0..1
        SetMasterVolume(linear01);
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, linear01);
        PlayerPrefs.Save();
    }

    void SetMasterVolume(float linear01)
    {
        if (audioMixer == null) return;
        // convert linear 0..1 to decibels; avoid log of zero
        float dB;
        if (linear01 <= 0.0001f) dB = -80f;
        else dB = Mathf.Log10(Mathf.Clamp(linear01, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(masterVolumeParam, dB);
    }
    #endregion

    #region Controls
    public void OnMouseSensitivityChanged(float val)
    {
        PlayerPrefs.SetFloat(PREF_MOUSE_SENS, val);
        PlayerPrefs.Save();
        // Other scripts should read SettingsManager.GetMouseSensitivity() when needed
    }

    public void OnInvertYChanged(bool invert)
    {
        PlayerPrefs.SetInt(PREF_INVERT_Y, invert ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnDisableShakeChanged(bool disabled)
    {
        PlayerPrefs.SetInt(PREF_DISABLE_SHAKE, disabled ? 1 : 0);
        PlayerPrefs.Save();
        OnShakeToggled?.Invoke(disabled);
    }

    public static float GetMouseSensitivity()
    {
        return PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 1f);
    }
    public static bool GetInvertY()
    {
        return PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1;
    }
    public static bool GetDisableShake()
    {
        return PlayerPrefs.GetInt(PREF_DISABLE_SHAKE, 0) == 1;
    }
    #endregion

    #region Rebinding (New Input System)
    // Start an interactive rebind for the given RebindEntry
    public void StartRebind(RebindEntry entry)
    {
        if (playerInput == null)
        {
            Debug.LogWarning("SettingsManager: PlayerInput is null — cannot start rebind.");
            return;
        }

        var actions = playerInput.actions;
        if (actions == null)
        {
            Debug.LogWarning("SettingsManager: PlayerInput.actions is null.");
            return;
        }

        var action = actions.FindAction(entry.actionName, true);
        if (action == null)
        {
            Debug.LogWarning($"SettingsManager: action '{entry.actionName}' not found in PlayerInput actions.");
            return;
        }

        // If a rebind is already running, cancel it
        activeRebindOp?.Cancel();
        activeRebindOp?.Dispose();

        // Optionally clear the current override on that binding first so the new binding replaces it
        if (entry.bindingIndex >= 0 && entry.bindingIndex < action.bindings.Count)
            action.RemoveBindingOverride(entry.bindingIndex);

        // Create the operation
        action.Disable();
        activeRebindOp = action.PerformInteractiveRebinding(entry.bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>/position")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                op.Dispose();
                activeRebindOp = null;
                action.Enable();
                // Save rebinds to PlayerPrefs
                SaveRebinds();

                // Update display text
                RefreshBindingDisplay(entry);
            })
            .OnCancel(op =>
            {
                op.Dispose();
                activeRebindOp = null;
                RefreshBindingDisplay(entry);
                action.Enable();
            })
            .Start();

        // Feedback to UI: show "Press a key..."
        if (entry.displayText != null) entry.displayText.text = "Press a key... (Esc to cancel)";
    }

    // Save current binding overrides for the whole asset
    public void SaveRebinds()
    {
        if (playerInput == null || playerInput.actions == null) return;
        try
        {
            var rebindJson = playerInput.actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PREF_REBINDS, rebindJson);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("SettingsManager: Failed to save rebinds: " + e.Message);
        }
    }

    // Load rebinds from PlayerPrefs (call from Awake / settings load)
    public void LoadRebinds()
    {
        if (playerInput == null || playerInput.actions == null) return;
        var json = PlayerPrefs.GetString(PREF_REBINDS, string.Empty);
        if (string.IsNullOrEmpty(json)) return; // nothing to load

        try
        {
            playerInput.actions.LoadBindingOverridesFromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SettingsManager: Failed to load rebinds: " + e.Message);
        }
    }

    public void ResetAllBindingsToDefault()
    {
        if (playerInput == null || playerInput.actions == null) return;

        playerInput.actions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PREF_REBINDS);
        PlayerPrefs.Save();
        RefreshAllBindingDisplays();
    }

    void RefreshAllBindingDisplays()
    {
        foreach (var entry in rebindEntries)
            RefreshBindingDisplay(entry);
    }

    void RefreshBindingDisplay(RebindEntry entry)
    {
        if (entry.displayText == null || playerInput == null || playerInput.actions == null) return;

        var action = playerInput.actions.FindAction(entry.actionName, true);
        if (action == null)
        {
            entry.displayText.text = "(action not found)";
            return;
        }

        try
        {
            // If you want the binding for a particular index
            if (entry.bindingIndex >= 0 && entry.bindingIndex < action.bindings.Count)
            {
                var binding = action.bindings[entry.bindingIndex];
                // Use InputControlPath.ToHumanReadableString for nicer text
                string display = InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                if (string.IsNullOrEmpty(display))
                    display = action.GetBindingDisplayString(entry.bindingIndex);
                if (string.IsNullOrEmpty(display))
                    display = "(unbound)";
                entry.displayText.text = display;
            }
            else
            {
                // Fallback: display the primary binding string for the action
                var display = action.GetBindingDisplayString();
                entry.displayText.text = string.IsNullOrEmpty(display) ? "(unbound)" : display;
            }
        }
        catch (Exception)
        {
            entry.displayText.text = action.GetBindingDisplayString();
        }
    }
    #endregion

    #region Save / Load All Settings
    public void SaveAllSettings()
    {
        PlayerPrefs.Save();
        SaveRebinds();
    }

    public void LoadAllSettings()
    {
        // Fullscreen: default to true if player has never set anything (so game starts fullscreen unless explicitly toggled)
        bool fs;
        if (PlayerPrefs.HasKey(PREF_FULLSCREEN))
        {
            fs = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
        }
        else
        {
            fs = true; // default behavior you requested
        }

        // Apply fullscreen
        if (fs)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }

        if (fullscreenToggle != null) fullscreenToggle.isOn = fs;

        // VSync
        bool vs = PlayerPrefs.GetInt(PREF_VSYNC, QualitySettings.vSyncCount == 1 ? 1 : 0) == 1;
        QualitySettings.vSyncCount = vs ? 1 : 0;
        if (vSyncToggle != null) vSyncToggle.isOn = vs;

        // Resolution index
        int resIndex = PlayerPrefs.GetInt(PREF_RESOLUTION_INDEX, 0);
        if (availableResolutions != null && availableResolutions.Length > 0 && resIndex >= 0 && resIndex < availableResolutions.Length)
        {
            var r = availableResolutions[resIndex];
            // Use FullScreenWindow if fullscreen active, otherwise windowed
            var fullscreenMode = Screen.fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(r.width, r.height, fullscreenMode, r.refreshRate);

            if (resolutionDropdownTMP != null) { resolutionDropdownTMP.value = resIndex; resolutionDropdownTMP.RefreshShownValue(); }
            if (resolutionDropdown != null) { resolutionDropdown.value = resIndex; resolutionDropdown.RefreshShownValue(); }
        }

        // Master volume
        float master = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f);
        if (masterVolumeSlider != null) masterVolumeSlider.value = master;
        SetMasterVolume(master);

        // Mouse sens
        float sens = PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 1f);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = sens;

        // Invert Y
        bool inv = PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1;
        if (invertYToggle != null) invertYToggle.isOn = inv;

        // disable shake
        bool ds = PlayerPrefs.GetInt(PREF_DISABLE_SHAKE, 0) == 1;
        if (disableShakeToggle != null) disableShakeToggle.isOn = ds;
        OnShakeToggled?.Invoke(ds);

        // Framerate
        int storedFrame = PlayerPrefs.GetInt(PREF_FRAMERATE, 60);
        int idxFrame = Array.IndexOf(framerateOptionValues, storedFrame);
        if (idxFrame < 0) idxFrame = Array.IndexOf(framerateOptionValues, 60);
        if (framerateDropdownTMP != null)
        {
            framerateDropdownTMP.value = idxFrame;
            framerateDropdownTMP.RefreshShownValue();
        }
        if (framerateDropdown != null)
        {
            framerateDropdown.value = idxFrame;
            framerateDropdown.RefreshShownValue();
        }
        Application.targetFrameRate = (storedFrame == 0) ? -1 : storedFrame;

        // Load rebinds
        LoadRebinds();
    }
    #endregion

#if UNITY_EDITOR
    // Helpful quick reset from inspector while developing
    [ContextMenu("Reset All PlayerPrefs (settings)")]
    void ResetAllPrefs()
    {
        PlayerPrefs.DeleteKey(PREF_MASTER_VOLUME);
        PlayerPrefs.DeleteKey(PREF_MOUSE_SENS);
        PlayerPrefs.DeleteKey(PREF_INVERT_Y);
        PlayerPrefs.DeleteKey(PREF_DISABLE_SHAKE);
        PlayerPrefs.DeleteKey(PREF_FULLSCREEN);
        PlayerPrefs.DeleteKey(PREF_VSYNC);
        PlayerPrefs.DeleteKey(PREF_RESOLUTION_INDEX);
        PlayerPrefs.DeleteKey(PREF_REBINDS);
        PlayerPrefs.DeleteKey(PREF_FRAMERATE);
        PlayerPrefs.Save();
        Debug.Log("SettingsManager: Cleared settings PlayerPrefs.");
    }
#endif
}
