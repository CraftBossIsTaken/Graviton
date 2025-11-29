using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScaleUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScale = 1.15f;
    public float scaleSpeed = 8f;
    public AudioSource a;
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smooth scaling
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.deltaTime * scaleSpeed
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CompareTag("Diff"))
            targetScale = originalScale * hoverScale;
            a.Play();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CompareTag("Diff"))
            targetScale = originalScale;
    }
}
