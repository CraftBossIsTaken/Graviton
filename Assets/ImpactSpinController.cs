using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ImpactSpinController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraRig;        // camera root for local shake
    public Transform cinemachineTarget; // hard-lock target for main camera
    public AudioSource au;
    public static ImpactSpinController Instance { get; private set; }

    [Header("Shake Settings")]
    public float shakeMagnitude = 0.5f;
    public float spinSpeed = 720f;       // degrees per second on Y
    public float zWobbleAngle = 15f;     // max rotation on Z
    public int zWobbleCycles = 2;        // how many times it swings back and forth
    public float duration = 0.5f;        // total duration of effect
    public AnimationCurve dropOff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    Vector3 initialCamPos;
    Quaternion initialCineRot;
    float accumulatedSpin = 0f;

    void Awake()
    {
        if (cinemachineTarget) initialCamPos = cinemachineTarget.localPosition;
        if (cinemachineTarget) initialCineRot = cinemachineTarget.localRotation;
          if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
       
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
            StartImpact();
    }
#endif

    public void StartImpact()
    {
        StartCoroutine(ShakeAndSpinCoroutine());
        if (au) au.Play();
    }

    IEnumerator ShakeAndSpinCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float amt = dropOff.Evaluate(t);

            // Compute blend for last 15%
            float blend = 1f;
            if (t > 0.85f)
                blend = Mathf.InverseLerp(1f, 0.85f, t); // 1 -> 0

            // Camera shake: random each frame
            Vector3 shake = Random.insideUnitSphere * shakeMagnitude * amt * blend;
            if (cinemachineTarget)
                cinemachineTarget.localPosition = initialCamPos + shake;

            // Accumulate Y spin continuously
            accumulatedSpin += spinSpeed * amt * Time.deltaTime;

            // Z wobble: sine wave decays in last 15%
            float zRot = Mathf.Sin(t * Mathf.PI * zWobbleCycles * 2f) * zWobbleAngle * amt * blend;

            if (cinemachineTarget)
                cinemachineTarget.localRotation = initialCineRot * Quaternion.Euler(0, accumulatedSpin, zRot);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Final position & rotation stays where it ended (no snap)
    }
}
