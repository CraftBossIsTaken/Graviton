using UnityEngine;
using System.Collections;

using Cinemachine;


public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Fallback shake settings")]
    public float fallbackMagnitude = 0.2f;
    public float fallbackFrequency = 20f;

    CinemachineImpulseSource impulseSource;
    Transform cameraRoot;
    Coroutine fallbackShake;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;

        // find Cinemachine Impulse Source if present on the same object or in scene
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
        {
            impulseSource = FindObjectOfType<CinemachineImpulseSource>();
        }

        cameraRoot = Camera.main != null ? Camera.main.transform : null;
    }

    /// <summary>
    /// Public call to shake the camera. Will try Cinemachine first, otherwise fallback.
    /// </summary>
    public void Shake(float magnitude, float duration)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(magnitude);
        }
        else
        {
            if (fallbackShake != null) StopCoroutine(fallbackShake);
            fallbackShake = StartCoroutine(FallbackShake(magnitude, duration));
        }
    }

    IEnumerator FallbackShake(float magnitude, float duration)
    {
        float elapsed = 0f;
        Vector3 originalPos = cameraRoot != null ? cameraRoot.localPosition : Vector3.zero;

        while (elapsed < duration)
        {
            float damper = 1.0f - Mathf.Clamp01(elapsed / duration);
            float x = (Mathf.PerlinNoise(Time.time * fallbackFrequency, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, Time.time * fallbackFrequency) - 0.5f) * 2f;
            Vector3 offset = new Vector3(x, y, 0f) * fallbackMagnitude * magnitude * damper;

            if (cameraRoot != null) cameraRoot.localPosition = originalPos + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cameraRoot != null) cameraRoot.localPosition = originalPos;
    }
}
