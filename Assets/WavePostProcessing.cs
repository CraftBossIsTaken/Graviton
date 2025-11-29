using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Cinemachine;

public class WavePostProcessing : MonoBehaviour
{
    [Header("References")]
    [Tooltip("A global Volume in the scene (URP/HDRP). Its profile should include Vignette, ColorAdjustments, ChromaticAberration, FilmGrain, LensDistortion, etc.")]
    public Volume volume;
    [Tooltip("Cinemachine virtual camera to adjust FOV and noise")]
    public CinemachineVirtualCamera virtualCam;

    [Header("Effect targets (max values applied when gravityMultiplier indicates 'very strong')")]
    [Range(0f, 1f)] public float vignetteMax = 0.5f;
    [Range(0f, 1f)] public float chromaMax = 0.5f;
    [Range(0f, 1f)] public float filmGrainMax = 0.6f;
    [Tooltip("Saturation reduction in perceptual points (negative = desaturate)")]
    public float saturationMax = -40f;
    [Range(-1f, 1f)] public float lensDistortionMax = 0.15f;
    [Tooltip("How much to change FOV (negative = tighten)")]
    public float fovDeltaMax = -6f;
    [Tooltip("Cinemachine noise amplitude at max intensity")]
    public float noiseAmplitudeMax = 3.0f;
    [Tooltip("Seconds to lerp visual in/out")]
    public float lerpDuration = 1.0f;

    // internals
    VolumeComponent _vignetteComp;
    VolumeComponent _chromaComp;
    VolumeComponent _filmGrainComp;
    VolumeComponent _colorAdjComp;
    VolumeComponent _lensDistortionComp;
    CinemachineBasicMultiChannelPerlin _camNoise;

    // store original parameter values so we can revert
    Dictionary<string, float> _originals = new Dictionary<string, float>();
    Coroutine _activeRoutine = null;
    Coroutine _autoRevertRoutine = null;

    void OnEnable()
    {
        GravitationalWaveManager.OnWaveStart += HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd += HandleWaveEnd;
    }

    void OnDisable()
    {
        GravitationalWaveManager.OnWaveStart -= HandleWaveStart;
        GravitationalWaveManager.OnWaveEnd -= HandleWaveEnd;
    }

    void Start()
    {
        if (volume == null)
            Debug.LogWarning("[WavePostProcessing] Volume reference is null. Drag a Volume (with a profile) into the inspector.");

        if (virtualCam == null)
            Debug.LogWarning("[WavePostProcessing] virtualCam is null. Assign your CinemachineVirtualCamera.");

        // grab Cinemachine noise if present
        if (virtualCam != null)
            _camNoise = virtualCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        // try to find common components by name in the profile's components list
        if (volume != null && volume.profile != null)
        {
            foreach (var comp in volume.profile.components)
            {
                if (comp == null) continue;
                string tn = comp.GetType().Name.ToLowerInvariant();
                if (tn.Contains("vignette")) _vignetteComp = comp;
                if (tn.Contains("chromatic") || tn.Contains("chroma")) _chromaComp = comp;
                if (tn.Contains("film") || tn.Contains("filmgrain")) _filmGrainComp = comp;
                if (tn.Contains("color") || tn.Contains("coloradjustments") || tn.Contains("coloradjust")) _colorAdjComp = comp;
                if (tn.Contains("lens") || tn.Contains("distortion")) _lensDistortionComp = comp;
            }
        }

        // cache original parameter values for those we found
        CacheOriginal("_vignette_intensity", _vignetteComp, "intensity");
        CacheOriginal("_chroma_intensity", _chromaComp, "intensity");
        CacheOriginal("_film_intensity", _filmGrainComp, "intensity");
        CacheOriginal("_color_saturation", _colorAdjComp, "saturation");
        CacheOriginal("_lens_intensity", _lensDistortionComp, "intensity");
        if (_camNoise != null)
        {
            _originals["_cam_noise_amp"] = _camNoise.m_AmplitudeGain;
            _originals["_vcam_fov"] = virtualCam != null ? virtualCam.m_Lens.FieldOfView : 60f;
        }
        else if (virtualCam != null)
        {
            _originals["_vcam_fov"] = virtualCam.m_Lens.FieldOfView;
        }
    }

    // caches a float VolumeParameter named paramName inside component comp (if present)
    void CacheOriginal(string key, VolumeComponent comp, string paramName)
    {
        if (comp == null) return;
        FieldInfo f = comp.GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f == null)
        {
            // try common alternate names
            f = comp.GetType().GetField("m_" + paramName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) return;
        }
        object paramObj = f.GetValue(comp);
        if (paramObj == null) return;
        PropertyInfo pv = paramObj.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        if (pv == null) return;
        object val = pv.GetValue(paramObj);
        if (val is float fl)
            _originals[key] = fl;
    }

    // set a float param by reflection (if present)
    void SetVolumeFloat(VolumeComponent comp, string paramName, float newValue)
    {
        if (comp == null) return;
        FieldInfo f = comp.GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f == null)
            f = comp.GetType().GetField("m_" + paramName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f == null) return;
        object paramObj = f.GetValue(comp);
        if (paramObj == null) return;
        PropertyInfo pv = paramObj.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        if (pv == null) return;
        // attempt set
        pv.SetValue(paramObj, Convert.ChangeType(newValue, pv.PropertyType));
    }

    // Helper: get original or fallback
    float GetOriginal(string key, float fallback)
    {
        if (_originals.TryGetValue(key, out float v)) return v;
        return fallback;
    }

    // Event handlers
    void HandleWaveStart(GravitationalWaveManager.GravitationalWavePreset preset)
    {
        if (preset == null) return;
        // Stop any previous routines
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        if (_autoRevertRoutine != null) StopCoroutine(_autoRevertRoutine);

        // determine intensity from gravityMultiplier (1 => no change, >1 => stronger)
        float intensity = Mathf.Clamp01(Mathf.Abs(preset.gravityMultiplier - 1f)); // 0..1
        bool stronger = preset.gravityMultiplier > 1f;

        // If preset requests a stronger-gravity feel, use intensity as-is; if <1 (weaker gravity) invert targets.
        _activeRoutine = StartCoroutine(TransitionVisuals(preset, intensity, stronger, true));

        // schedule auto revert after duration (in case explicit end isn't called)
        _autoRevertRoutine = StartCoroutine(AutoRevertAfter(preset.duration));
    }

    void HandleWaveEnd(GravitationalWaveManager.GravitationalWavePreset preset)
    {
        if (preset == null) return;
        // Explicit revert: smoothly interpolate back to original
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        if (_autoRevertRoutine != null) StopCoroutine(_autoRevertRoutine);
        _activeRoutine = StartCoroutine(TransitionVisuals(preset, 0f, true, false)); // target intensity 0 (revert)
    }

    IEnumerator AutoRevertAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        // revert visuals
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(TransitionVisuals(null, 0f, true, false));
        _autoRevertRoutine = null;
    }

    // core transition coroutine: if "in" is true, we lerp from current -> target; if false, we lerp to originals (target intensity=0)
    IEnumerator TransitionVisuals(GravitationalWaveManager.GravitationalWavePreset preset, float intensity, bool stronger, bool @in)
    {
        float from = 0f, to = 1f;
        float elapsed = 0f;
        // read current parameter values as start
        float startVig = _vignetteComp != null ? ReadVolumeFloat(_vignetteComp, "intensity", GetOriginal("_vignette_intensity", 0f)) : 0f;
        float startChroma = _chromaComp != null ? ReadVolumeFloat(_chromaComp, "intensity", GetOriginal("_chroma_intensity", 0f)) : 0f;
        float startFilm = _filmGrainComp != null ? ReadVolumeFloat(_filmGrainComp, "intensity", GetOriginal("_film_intensity", 0f)) : 0f;
        float startSat = _colorAdjComp != null ? ReadVolumeFloat(_colorAdjComp, "saturation", GetOriginal("_color_saturation", 0f)) : 0f;
        float startLens = _lensDistortionComp != null ? ReadVolumeFloat(_lensDistortionComp, "intensity", GetOriginal("_lens_intensity", 0f)) : 0f;
        float startFOV = virtualCam != null ? virtualCam.m_Lens.FieldOfView : 60f;
        float startNoise = _camNoise != null ? _camNoise.m_AmplitudeGain : 0f;

        // desired targets (scale by intensity and by "stronger" sign)
        // stronger gravity -> more vignette/chroma/film, more desaturation (more negative)
        float targetVig = Mathf.Lerp(GetOriginal("_vignette_intensity", 0f), vignetteMax * intensity * (stronger ? 1f : 0.6f), @in ? 1f : 0f);
        float targetChroma = Mathf.Lerp(GetOriginal("_chroma_intensity", 0f), chromaMax * intensity * (stronger ? 1f : 0.6f), @in ? 1f : 0f);
        float targetFilm = Mathf.Lerp(GetOriginal("_film_intensity", 0f), filmGrainMax * intensity * (stronger ? 1f : 0.6f), @in ? 1f : 0f);
        float targetSat = Mathf.Lerp(GetOriginal("_color_saturation", 0f), GetOriginal("_color_saturation", 0f) + (saturationMax * intensity * (stronger ? 1f : 0.6f)), @in ? 1f : 0f);
        float targetLens = Mathf.Lerp(GetOriginal("_lens_intensity", 0f), lensDistortionMax * intensity * (stronger ? 1f : 0.6f), @in ? 1f : 0f);
        float targetFOV = virtualCam != null ? Mathf.Lerp(startFOV, GetOriginal("_vcam_fov", startFOV) + (fovDeltaMax * intensity * (stronger ? 1f : 0.5f)), @in ? 1f : 0f) : startFOV;
        float targetNoise = Mathf.Lerp(startNoise, noiseAmplitudeMax * intensity * (stronger ? 1f : 0.6f), @in ? 1f : 0f);

        // If we're reverting (@in==false), targets are originals (we already set GetOriginal accordingly by using intensity=0 above).
        float duration = lerpDuration;
        if (!@in) duration = lerpDuration; // same duration to revert

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float a = Mathf.SmoothStep(0f, 1f, t);

            SetIf(_vignetteComp, "intensity", Mathf.Lerp(startVig, targetVig, a));
            SetIf(_chromaComp, "intensity", Mathf.Lerp(startChroma, targetChroma, a));
            SetIf(_filmGrainComp, "intensity", Mathf.Lerp(startFilm, targetFilm, a));
            SetIf(_colorAdjComp, "saturation", Mathf.Lerp(startSat, targetSat, a));
            SetIf(_lensDistortionComp, "intensity", Mathf.Lerp(startLens, targetLens, a));

            if (virtualCam != null)
                virtualCam.m_Lens.FieldOfView = Mathf.Lerp(startFOV, targetFOV, a);

            if (_camNoise != null)
                _camNoise.m_AmplitudeGain = Mathf.Lerp(startNoise, targetNoise, a);

            yield return null;
        }

        // final clamp to exact target
        SetIf(_vignetteComp, "intensity", targetVig);
        SetIf(_chromaComp, "intensity", targetChroma);
        SetIf(_filmGrainComp, "intensity", targetFilm);
        SetIf(_colorAdjComp, "saturation", targetSat);
        SetIf(_lensDistortionComp, "intensity", targetLens);
        if (virtualCam != null) virtualCam.m_Lens.FieldOfView = targetFOV;
        if (_camNoise != null) _camNoise.m_AmplitudeGain = targetNoise;

        _activeRoutine = null;
    }

    // safe setters/readers
    float ReadVolumeFloat(VolumeComponent comp, string paramName, float fallback)
    {
        if (comp == null) return fallback;
        FieldInfo f = comp.GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f == null)
            f = comp.GetType().GetField("m_" + paramName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f == null) return fallback;
        object p = f.GetValue(comp);
        if (p == null) return fallback;
        PropertyInfo pv = p.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        if (pv == null) return fallback;
        object val = pv.GetValue(p);
        if (val is float fval) return fval;
        return fallback;
    }

    void SetIf(VolumeComponent comp, string paramName, float newValue)
    {
        if (comp == null) return;
        try
        {
            SetVolumeFloat(comp, paramName, newValue);
        }
        catch (Exception e)
        {
            // fail silently — not all components have the param
            Debug.LogWarning($"WavePostProcessing: failed to set {paramName} on {comp.GetType().Name}: {e.Message}");
        }
    }
}
