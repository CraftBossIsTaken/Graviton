using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class hS : MonoBehaviour
{
    [Tooltip("When true, radar visuals (Renderers / Canvases) will be hidden in the Scene view when NOT playing.")]
    public bool hideInEditor = true;

    // cached components
    Renderer[] cachedRenderers;
    Canvas[] cachedCanvases;

    void OnEnable()
    {
        CacheComponents();
        UpdateVisibility();

#if UNITY_EDITOR
        // listen for entering/exiting play mode so we restore visibility correctly
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
    }

    void CacheComponents()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedCanvases = GetComponentsInChildren<Canvas>(true);
    }

    void UpdateVisibility()
    {
        // If not hiding in editor, ensure everything is visible
        if (!hideInEditor)
        {
            SetEnabledForAll(true);
            return;
        }

        bool shouldShow = Application.isPlaying; // show only while playing
        SetEnabledForAll(shouldShow);
    }

    void SetEnabledForAll(bool enabled)
    {
        if (cachedRenderers != null)
        {
            foreach (var r in cachedRenderers)
                if (r) r.enabled = enabled;
        }

        if (cachedCanvases != null)
        {
            foreach (var c in cachedCanvases)
                if (c) c.enabled = enabled;
        }
    }

#if UNITY_EDITOR
    void OnPlayModeChanged(PlayModeStateChange state)
    {
        // When playmode state changes, re-cache (in case objects were created/removed)
        CacheComponents();
        UpdateVisibility();
    }

    // Provide an editor menu command to force-refresh (handy)
    [MenuItem("Tools/Radar/Refresh HideRadarInSceneView")]
    private static void RefreshAll()
    {
        foreach (var obj in FindObjectsOfType<hS>())
            obj.CacheComponents();
        Debug.Log("Refreshed HideRadarInSceneView caches.");
    }
#endif

    // In editor, Update runs frequently; keep it cheap. Only re-run visibility if not playing.
    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // keep visibility in sync if user toggles hideInEditor in inspector
            UpdateVisibility();
        }
#endif
    }
}
