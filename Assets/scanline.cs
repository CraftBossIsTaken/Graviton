using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
public class TMP3DHologramScanline : MonoBehaviour
{
    
    [Header("Scanline (horizontal)")]
    public float frequency = 8f;     // how many horizontal bands (higher = more bands)
    public float speed = 1f;         // vertical movement speed of bands
    public float contrast = 0.6f;    // brightness variation (0..1)

    [Header("Alpha / Hologram Flicker")]
    public float baseAlpha = 0.6f;       // base transparency
    public float alphaVariation = 0.45f; // how much transparency oscillates
    public float flickerSpeed = 2f;      // how fast alpha flickers

    [Header("Vertical columns (disappear effect)")]
    public int columns = 18;             // how many vertical columns across world X
    public float columnNoiseScale = 1.0f; // scale for Perlin noise across columns
    public float columnFadeThreshold = 0.45f; // threshold: lower -> more columns disappear
    public float columnFadeSmoothness = 0.2f; // smoothness of fade edges
    public float columnTimeSpeed = 0.8f; // how quickly column pattern evolves

    [Header("Coloring")]
    public Color colorA = new Color(0.1f, 1f, 1f); // cyan
    public Color colorB = new Color(0.3f, 0.6f, 1f); // blue
    public float colorShiftSpeed = 0.6f; // speed of color shifting

    [Header("Quality")]
    public bool skipWhenNotVisible = true; // skip updates if renderer not visible (good perf)

    private TextMeshPro tmp;
    private Mesh mesh;
    private Color32[] colors;
    private Renderer textRenderer;

    void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
        textRenderer = tmp.renderer;

        // duplicate material instance so changes are local
        tmp.fontMaterial = new Material(tmp.fontMaterial);
        // Ensure material behaves nicely for transparent holograms
        tmp.fontMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        tmp.fontMaterial.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);
        tmp.fontMaterial.SetInt("_ZWrite", 0);
    }

    void Update()
    {
        if (!tmp || !tmp.isActiveAndEnabled) return;
        if (skipWhenNotVisible && textRenderer != null && !textRenderer.isVisible) return;

        tmp.ForceMeshUpdate();
        mesh = tmp.mesh;
        var vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0) return;

        if (colors == null || colors.Length != vertices.Length)
            colors = new Color32[vertices.Length];

        float t = Time.time;

        // Precompute some constants
        float colInv = Mathf.Max(1, columns);
        float colScale = columnNoiseScale;

        for (int i = 0; i < vertices.Length; i++)
        {
            // World space position of vertex (keeps pattern consistent under transforms)
            Vector3 worldPos = transform.TransformPoint(vertices[i]);
            float yWorld = worldPos.y;
            float xWorld = worldPos.x;

            // === Horizontal brightness band ===
            // Create a moving band using PingPong for smooth repeat
            float pattern = Mathf.PingPong(yWorld * frequency + t * speed, 1f);
            float brightness = Mathf.Lerp(1f - contrast, 1f, pattern); // 0..1

            // === Alpha pulsing / flicker ===
            float alphaWave = baseAlpha + Mathf.Sin(yWorld * (frequency * 0.5f) + t * flickerSpeed * 2f) * (alphaVariation * 0.5f);
            // add a small global temporal jitter
            alphaWave += (Mathf.PerlinNoise(t * 3.3f, xWorld * 0.1f) - 0.5f) * 0.04f;
            alphaWave = Mathf.Clamp01(alphaWave);

            // === Vertical column disappearance ===
            // Use Perlin noise sampled along X to produce vertical stripe chance that evolves in time
            // Scale x by number of columns so we get distinct stripes across the text
            float sampleX = (xWorld * colInv) * colScale;
            float per = Mathf.PerlinNoise(sampleX + t * columnTimeSpeed, 0f); // 0..1
            // Smooth fade factor: if per < threshold -> column fades out; else visible
            float colFactor = Mathf.SmoothStep(0f, 1f, (per - columnFadeThreshold) / Mathf.Max(0.0001f, columnFadeSmoothness));
            // column factor in [0..1] multiplies alpha
            float finalAlpha = alphaWave * colFactor;

            // === Color shift ===
            // Mix between colorA and colorB with a time/x dependent LFO for shimmer
            float colorLerp = (Mathf.Sin(t * colorShiftSpeed + xWorld * 3f) + 1f) * 0.5f;
            Color baseCol = Color.Lerp(colorA, colorB, colorLerp);

            // Combine brightness into color and apply alpha
            float finalR = Mathf.Clamp01(baseCol.r * brightness);
            float finalG = Mathf.Clamp01(baseCol.g * brightness);
            float finalB = Mathf.Clamp01(baseCol.b * brightness);
            byte rb = (byte)(finalR * 255f);
            byte gb = (byte)(finalG * 255f);
            byte bb = (byte)(finalB * 255f);
            byte ab = (byte)(Mathf.Clamp01(finalAlpha) * 255f);

            colors[i] = new Color32(rb, gb, bb, ab);
        }

        mesh.colors32 = colors;
        tmp.UpdateGeometry(mesh, 0);
    }
}
