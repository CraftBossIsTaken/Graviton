using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIRaycastDebugger : MonoBehaviour
{
    [Header("Assign References")]
    public GraphicRaycaster raycaster; // Canvas GraphicRaycaster
    public EventSystem eventSystem;    // Scene EventSystem
    public Camera uiCamera;            // Needed if Canvas is Screen Space - Camera

    [Header("Debug Options")]
    public bool highlightTopHit = true;
    public Color highlightColor = Color.yellow;
    private GameObject lastHighlight;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            DebugUIRaycast();
        }
    }

    void DebugUIRaycast()
    {
        if (!raycaster || !eventSystem)
        {
            Debug.LogWarning("Raycaster or EventSystem not assigned!");
            return;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        Debug.Log("==== UI Raycast Debug ====");
        Debug.Log("Pointer Position: " + pointerData.position);
        Debug.Log("Number of UI elements hit: " + results.Count);

        if (results.Count == 0)
        {
            Debug.Log("No UI element is blocking the raycast.");
            return;
        }

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var go = result.gameObject;
            Debug.Log($"Hit {i}: {go.name} | Depth: {result.depth}", go);

            // Check Graphic component
            var graphic = go.GetComponent<Graphic>();
            if (graphic != null)
            {
                Debug.Log($"    Graphic Raycast Target: {graphic.raycastTarget}");
            }
            else
            {
                Debug.Log("    No Graphic component found!");
            }

            // Check CanvasGroup
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                Debug.Log($"    CanvasGroup: Interactable={cg.interactable}, BlocksRaycasts={cg.blocksRaycasts}, IgnoreParentGroups={cg.ignoreParentGroups}");
            }

            // Check Button/Toggle/Slider
            var button = go.GetComponent<Button>();
            if (button != null)
                Debug.Log($"    Button Interactable: {button.interactable}");

            var toggle = go.GetComponent<Toggle>();
            if (toggle != null)
                Debug.Log($"    Toggle Interactable: {toggle.interactable} | IsOn: {toggle.isOn}");

            var slider = go.GetComponent<Slider>();
            if (slider != null)
                Debug.Log($"    Slider Interactable: {slider.interactable} | Value: {slider.value}");

            // Log hierarchy path
            Debug.Log("    Hierarchy Path: " + GetHierarchyPath(go));
        }

        // Optional: highlight top hit
        if (highlightTopHit)
        {
            HighlightTopHit(results[0].gameObject);
        }
    }

    string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    void HighlightTopHit(GameObject go)
    {
        if (lastHighlight != null)
        {
            var oldImg = lastHighlight.GetComponent<Image>();
            if (oldImg != null)
                oldImg.color = Color.white; // reset
        }

        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = highlightColor;
            lastHighlight = go;
        }
    }
}
