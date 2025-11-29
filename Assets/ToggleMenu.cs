using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // or .HighDefinition, depending on project
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class ToggleMenu : MonoBehaviour
{
    public GameObject main;
    public GameObject settings;
    public GameObject diff;
    public Volume volume; // Assign in inspector
    private ColorAdjustments colorAdjust;
private const float minExp = -5.89f;
private const float maxExp = -1.89f;
public Image black;

    void Start()
    { 
          Time.timeScale= 1f;
        // Grab the exposure (Color Adjustments) from the volume
        if (volume != null && volume.profile.TryGet(out colorAdjust))
        {
            // Ensure we can modify exposure
            colorAdjust.postExposure.overrideState = true;
        }
    }
    public void LoadGame(string diff)
    {
      StartCoroutine(loadGameC(diff));
    }
    private IEnumerator loadGameC(string diff)
    {
        float t = 0;
        while(t < 1)
        {
            t += Time.deltaTime / 1f;
            Color c = black.color;
            c.a = Mathf.Lerp(0, 1, t);
            black.color = c;
 yield return null; 
        }

        switch(diff)
        {
            case "easy":
            SceneManager.LoadScene(1);
            break;
        }
    }
    public void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(settings.activeSelf)
            {
                Toggle(main);
            }
            if(diff.activeSelf)
            {
                Toggle(main);
                IncreaseExposure(4f);
            }
        }
    }
    public void Toggle(GameObject g)
    {
        main.SetActive(false);
        settings.SetActive(false);
        diff.SetActive(false);
        g.SetActive(true);
    }

    // -------- Exposure control --------
public void IncreaseExposure(float amount)
{
    if (colorAdjust != null)
    {
        float target = Mathf.Clamp(colorAdjust.postExposure.value + amount, minExp, maxExp);
        StartCoroutine(FadeExposure(target, 0.5f));
    }
}

public void DecreaseExposure(float amount)
{
    if (colorAdjust != null)
    {
        float target = Mathf.Clamp(colorAdjust.postExposure.value - amount, minExp, maxExp);
        StartCoroutine(FadeExposure(target, 0.5f));
    }
}


    private Coroutine exposureRoutine;

private IEnumerator FadeExposure(float target, float duration)
{
    // If another fade is running, stop it
    if (exposureRoutine != null)
        StopCoroutine(exposureRoutine);

    exposureRoutine = StartCoroutine(FadeExposureRoutine(target, duration));
    yield break;
}

private IEnumerator FadeExposureRoutine(float target, float duration)
{
    float start = colorAdjust.postExposure.value;
    float t = 0f;

    while (t < 1f)
    {
        t += Time.deltaTime / duration;

        float newValue = Mathf.Lerp(start, target, t);
        colorAdjust.postExposure.value = Mathf.Clamp(newValue, minExp, maxExp);

        yield return null;
    }

    exposureRoutine = null;
}


}
