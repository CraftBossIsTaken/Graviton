using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RadarAsteroidTracker (world-space 3D radar map)
/// - Scans for GameObjects tagged "Asteroid" within detectionRange around a world-space radar center.
/// - For each tracked asteroid instantiates a radar icon prefab as a child of radarContainer (world-space Transform)
///   and updates its localPosition on the radar plane (X/Z mapped to local X/Z) so the radar is a 3D world map.
/// - Estimates asteroid mesh size (by MeshFilter, SkinnedMeshRenderer or Renderer) and scales the icon accordingly.
///   If the asteroid's max mesh dimension <= 10 units, the radar prefab's default scale is kept.
/// - Destroys only the radar icon instances when their asteroid goes out of detectionRange (does NOT destroy the
///   world asteroid GameObjects).
/// USAGE:
/// 1) Attach to an empty GameObject in your scene.
/// 2) Set radarCenterPosition to your ship position (defaults to the coordinates you provided).
/// 3) Set radarContainer to a world-space Transform that represents the center of the radar map in the scene.
///    The script parents instantiated icons to radarContainer and places them on its local XZ plane.
/// 4) Set radarPrefab to a world-space prefab (e.g. small marker object). Its transform.localScale is treated as
///    the default scale.
/// 5) mapRadius is the world-space radius of the circular radar map. If left 0 it is computed as Abs(295.477-296.965)
///    per your earlier specification.
/// NOTES:
/// - This implementation assumes the radar map plane is the local XZ plane of radarContainer. If you need a different
///   orientation, adjust the mapping part where localOffset is created.
/// - If your radarContainer uses rotated transform, icons will follow that rotation because we use localPosition.
/// </summary>
public class tracker : MonoBehaviour
{
    [Header("Radar Settings")]
    [Tooltip("World-space center of the radar (the ship position).")]
    public Vector3 radarCenterPosition = new Vector3(-32.604f, 77.4829f, 295.477f);

    [Tooltip("Range in world units to search for asteroids.")]
    public float detectionRange = 300f;

    [Tooltip("World-space radius of the circular radar map (local XZ of radarContainer).")]
    public float mapRadius = 0f; // computed in Awake if left 0

    [Tooltip("Prefab used for black holes on the radar.")]
public GameObject blackHolePrefab;


    [Header("Prefab & container")]
    [Tooltip("Prefab used as a radar icon for each asteroid. Prefab's transform.localScale is used as 'default'.")]
    public GameObject radarPrefab;

    [Tooltip("World-space transform that represents the radar map's center/origin. Icons are parented to this andplaced on its local XZ plane.")]
    public Transform radarContainer;

    [Header("Behaviour")]
    [Tooltip("If true, any asteroid whose distance from radar center exceeds detectionRange will have its radar icon instance destroyed (the asteroid GameObject itself will NOT be destroyed).")]
    public bool destroyIconWhenOutOfRange = true;

    [Tooltip("How often (seconds) to refresh the scan. Set to 0 for every frame. Using >0 reduces CPU usage.")]
    public float scanInterval = 0.15f;

    [Tooltip("Local Y offset (above radar plane) to place icons. Useful to make icons hover slightly above the map.")]
    public float iconHeight = 0.2f;
    [Tooltip("Prefab used as a radar icon for each satellite (tag 'PowerSource').")]
public GameObject satellitePrefab;


    // internal bookkeeping: maps asteroid InstanceID -> icon instance
    Dictionary<int, GameObject> iconMap = new Dictionary<int, GameObject>();
    float nextScanTime = 0f;

    // cache default prefab scale
    Vector3 prefabDefaultScale = Vector3.one;

    void Awake()
    {
        if (mapRadius <= 0f)
        {
            // per user: radius = |(295.477 - 296.965)|
            mapRadius = Mathf.Abs(295.477f - 296.965f);
        }

        if (radarPrefab != null)
            prefabDefaultScale = radarPrefab.transform.localScale;

        if (radarContainer == null)
            Debug.LogWarning("RadarAsteroidTracker: radarContainer is not assigned. Icons will not be parented correctly.");
    }
void CleanupMissingAsteroids() { List<int> missing = new List<int>(); foreach (var kvp in iconMap) { if (kvp.Value == null) { missing.Add(kvp.Key); continue; } } foreach (int id in missing) iconMap.Remove(id); }
    void Update()
    {
        if (scanInterval <= 0f)
        {
            ScanAndUpdate();
        }
        else if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanAndUpdate();
        }

        // cleanup entries whose asteroid was destroyed externally
        CleanupMissingAsteroids();
    }

    void ScanAndUpdate()
{
    // Find asteroids and black holes
    List<GameObject> targets = new List<GameObject>();
  targets.AddRange(GameObject.FindGameObjectsWithTag("Asteroid"));
targets.AddRange(GameObject.FindGameObjectsWithTag("BlackHole"));
targets.AddRange(GameObject.FindGameObjectsWithTag("PowerSource")); // NEW

    HashSet<int> seenIds = new HashSet<int>();

    foreach (var obj in targets)
    {
        if (obj == null) continue;

        float distance = Vector3.Distance(obj.transform.position, radarCenterPosition);
        int instanceId = obj.GetInstanceID();

        // Out of range: remove icon
    if (distance > detectionRange)
{
    if (!obj.CompareTag("BlackHole") && !obj.CompareTag("PowerSource"))
    {
        if (destroyIconWhenOutOfRange && iconMap.TryGetValue(instanceId, out GameObject existingIcon) && existingIcon != null)
        {
            Destroy(existingIcon);
            iconMap.Remove(instanceId);
        }
        continue; // normal asteroids: stop updating if out of range
    }
    
    // For black holes: do NOT continue, so position and scaling still update
}



        seenIds.Add(instanceId);

        GameObject icon;
        if (!iconMap.TryGetValue(instanceId, out icon) || icon == null)
        {
            if (radarContainer == null)
            {
                Debug.LogWarning("Radar container not assigned.");
                return;
            }

            // choose prefab depending on tag
           GameObject prefabToUse;

if (obj.CompareTag("BlackHole"))
    prefabToUse = blackHolePrefab;
else if (obj.CompareTag("PowerSource"))
    prefabToUse = satellitePrefab;
else
    prefabToUse = radarPrefab; // default asteroid icon

            if (prefabToUse == null)
            {
                Debug.LogWarning($"Missing prefab for {obj.tag}.");
                continue;
            }

            icon = Instantiate(prefabToUse, radarContainer);
            icon.name = $"RadarIcon_{obj.name}_{instanceId}";
            iconMap[instanceId] = icon;
        }

        // --- same position mapping logic ---
        Vector3 localWorldOffset = obj.transform.position - radarCenterPosition;
        float mappedX = (localWorldOffset.x / detectionRange) * mapRadius;
        float mappedZ = (localWorldOffset.z / detectionRange) * mapRadius;
        Vector2 mapped2D = new Vector2(mappedX, mappedZ);
        if (mapped2D.magnitude > mapRadius) mapped2D = mapped2D.normalized * mapRadius;
        Vector3 localOffset = new Vector3(mapped2D.x, iconHeight, mapped2D.y);
        icon.transform.localPosition = localOffset;

        // --- same scaling logic ---
     // --- scaling rules ---
if (obj.CompareTag("BlackHole") || obj.CompareTag("PowerSource"))
{
    // Black holes and satellites (PowerSource): always use prefab's default scale
   
}
else
{
    // Asteroids: scale based on mesh size
    Vector3 worldMeshSize = EstimateWorldMeshSize(obj.transform);
    float maxDim = Mathf.Max(worldMeshSize.x, worldMeshSize.y, worldMeshSize.z);

    if (maxDim <= 0f || maxDim <= 10f)
        icon.transform.localScale = prefabDefaultScale;
    else
    {
        float scaleMultiplier = Mathf.Clamp(maxDim / 10f, 1f, 6f);
        icon.transform.localScale = prefabDefaultScale * scaleMultiplier;
    }
}

    }

    // cleanup code stays the same
List<int> toRemove = new List<int>();

foreach (var kvp in iconMap)
{
    GameObject icon = kvp.Value;
    if (icon == null)
    {
        toRemove.Add(kvp.Key);
        continue;
    }

    // Find the corresponding world object by instance ID
    GameObject worldObject = null;
    foreach (var go in GameObject.FindObjectsOfType<GameObject>())
    {
        if (go.GetInstanceID() == kvp.Key)
        {
            worldObject = go;
            break;
        }
    }

    // If the world object is gone (destroyed)
    if (worldObject == null)
    {
        Destroy(icon);
        toRemove.Add(kvp.Key);
        continue;
    }

    // If the world object still exists but wasn't seen and is NOT a BlackHole → remove
    if (!seenIds.Contains(kvp.Key) && !worldObject.CompareTag("BlackHole"))
    {
        Destroy(icon);
        toRemove.Add(kvp.Key);
    }
}

foreach (int id in toRemove)
    iconMap.Remove(id);
}


    /// <summary>
    /// Tries to estimate the mesh's world-space size by checking MeshFilter, SkinnedMeshRenderer or Renderer bounds.
    /// Returns Vector3.zero if nothing found.
    /// </summary>
    Vector3 EstimateWorldMeshSize(Transform t)
    {
        // try MeshFilter
        MeshFilter mf = t.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds; // local-space bounds
            Vector3 worldSize = Vector3.Scale(b.size, mf.transform.lossyScale);
            return worldSize;
        }

        // try SkinnedMeshRenderer
        SkinnedMeshRenderer smr = t.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            Bounds b = smr.sharedMesh.bounds;
            Vector3 worldSize = Vector3.Scale(b.size, smr.transform.lossyScale);
            return worldSize;
        }

        // last fallback: Renderer.bounds (axis-aligned world bounds) — more expensive but reliable
        Renderer rend = t.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds; // already in world space
            return b.size;
        }

        return Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(radarCenterPosition, detectionRange);

        if (radarContainer != null)
        {
            Gizmos.color = Color.green;
            // draw map circle in radarContainer's local XZ plane by sampling points and converting to world
            int segments = 64;
            Vector3 prev = radarContainer.TransformPoint(new Vector3(mapRadius, 0f, 0f));
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = radarContainer.TransformPoint(new Vector3(Mathf.Cos(ang) * mapRadius, 0f, Mathf.Sin(ang) * mapRadius));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
        else
        {
            // fallback: draw circle in world XZ around radarCenterPosition
            Gizmos.color = Color.yellow;
            int segments = 64;
            Vector3 prev = radarCenterPosition + new Vector3(mapRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = radarCenterPosition + new Vector3(Mathf.Cos(ang) * mapRadius, 0f, Mathf.Sin(ang) * mapRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
