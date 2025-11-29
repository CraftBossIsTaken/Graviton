using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class BlackHoleOrbitFollower : MonoBehaviour
{
    [Header("Core")]
    public Transform blackHole; // center of orbits (if null, uses this.transform)
    public bool generateOnStart = true;
    public bool clearBeforeGenerate = true;
    public int seed = 0; // used if useRandomSeed == false
    public bool useRandomSeed = true;
[Header("Vertical Variation")]
public float verticalJitter = 2f; // random Y offset range (± value)

    [Header("Global Prefabs")]
    public GameObject[] globalAsteroidPrefabs; // fallback list
    public GameObject satellitePrefab; // rare special object

    [Header("Runtime Options")]
    public bool parentObjectsUnderThis = true; // parent generated orbit parents under this object
    public bool spawnInEditMode = true; // allow regeneration while editing

    [Header("Orbits (one entry = one ring)")]
    public List<OrbitSettings> orbits = new List<OrbitSettings>();

    // internal
    private readonly List<GameObject> _orbitParents = new List<GameObject>();
    private System.Random _rng;

    void Start()
    {
        if (!Application.isPlaying && !spawnInEditMode) return; if (!Application.isPlaying) return;
        if (generateOnStart)
        {
            GenerateOrbits();
        }
    }

    [ContextMenu("Generate Orbits")]
    public void GenerateOrbits()
    {
        if (!blackHole) blackHole = this.transform;

        _rng = useRandomSeed ? new System.Random(System.Environment.TickCount) : new System.Random(seed);

        if (clearBeforeGenerate) ClearOrbits();

        float lastRadius = 0f;

        for (int i = 0; i < orbits.Count; i++)
        {
            var os = orbits[i];

            // compute radius if using relative distance
            float radius = os.radius;
            if (os.useRelativeDistance)
            {
                radius = lastRadius + os.distanceFromPrevious;
            }

            // small jitter so rings aren't perfectly circular
            radius += RandomRange(os.radiusJitter * -0.5f, os.radiusJitter * 0.5f);

            // create orbit parent
            var orbitParentGO = new GameObject(string.IsNullOrEmpty(os.name) ? $"Orbit_{i}" : $"Orbit_{i}_{os.name}");
            if (parentObjectsUnderThis) orbitParentGO.transform.SetParent(this.transform, false);
            orbitParentGO.transform.position = blackHole.position;
            orbitParentGO.transform.rotation = Quaternion.identity;
// After creating orbitParentGO
var trigger = orbitParentGO.AddComponent<SphereCollider>();
trigger.isTrigger = true;
trigger.radius = radius + (os.ringWidth / 2f); 
var orbitTrigger = orbitParentGO.AddComponent<OrbitTrigger>();
orbitTrigger.orbitRadius = radius;   // the computed radius of this orbit
orbitTrigger.axis = os.orbitAxis;    // orbit axis from OrbitSettings

            // add rotator to handle orbital motion
            var rot = orbitParentGO.AddComponent<OrbitRotator>();
            rot.degreesPerSecond = os.orbitSpeed;
            rot.rotateAxis = os.orbitAxis;

            _orbitParents.Add(orbitParentGO);

            // determine number of asteroids for this ring
            int count = os.density;
            if (os.useDensityJitter)
            {
                float jitter = RandomRange(-os.densityJitter, os.densityJitter);
                count = Mathf.Max(0, Mathf.RoundToInt(count * (1f + jitter)));
            }

            GenerateAsteroidsForOrbit(os, orbitParentGO.transform, radius, count, i);

            lastRadius = radius + os.ringWidth * 0.5f + os.distanceBufferAfter; // ensure breathing space
        }
    }

   // --------------------- REPLACEMENT METHOD ---------------------
void GenerateAsteroidsForOrbit(OrbitSettings os, Transform parent, float radius, int count, int orbitIndex)
{
    // quick safety
    if (count <= 0) return;

    // ensure radius is finite
    if (float.IsNaN(radius) || float.IsInfinity(radius))
    {
        Debug.LogWarning($"[OrbitGenerator] Invalid radius for orbit '{os.name}' (radius is NaN/Inf). Skipping orbit.");
        return;
    }

    var placedPositions = new List<Vector3>();

    for (int k = 0; k < count; k++)
    {
        bool placed = false;
        int attempts = 0;
        const int maxAttempts = 12;

        while (!placed && attempts < maxAttempts)
        {
            attempts++;

            float angle = (float)(_rng.NextDouble() * 360.0);
            float radialOffset = RandomRange(-os.ringWidth / 2f, os.ringWidth / 2f);

         // Create a rotation aligned with the orbit axis
Quaternion orbitRot = Quaternion.FromToRotation(Vector3.up, os.orbitAxis.normalized);
Vector3 localPos = orbitRot * (Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (radius + radialOffset));

// add slight Y offset (vertical jitter)
if (os.verticalJitter > 0f)
{
    float yOffset = RandomRange(-os.verticalJitter * 0.5f, os.verticalJitter * 0.5f);
    localPos += Vector3.up * yOffset;
}


            // guard: ensure localPos is finite
            if (!IsFinite(localPos))
            {
                Debug.LogWarning($"[OrbitGenerator] Computed non-finite localPos for orbit '{os.name}' (angle={angle}, radius={radius}, radialOffset={radialOffset}). Skipping this attempt.");
                continue;
            }

            // check spacing from already placed objects in this orbit
            bool tooClose = false;
            foreach (var p in placedPositions)
            {
                if (Vector3.Distance(p, localPos) < os.minSpacing) { tooClose = true; break; }
            }

            if (tooClose) continue; // try another position

            // choose prefab
            GameObject prefab = null;
            if (os.asteroidPrefabs != null && os.asteroidPrefabs.Length > 0)
            {
                prefab = os.asteroidPrefabs[_rng.Next(os.asteroidPrefabs.Length)];
            }
            else if (globalAsteroidPrefabs != null && globalAsteroidPrefabs.Length > 0)
            {
                prefab = globalAsteroidPrefabs[_rng.Next(globalAsteroidPrefabs.Length)];
            }

            // satellite chance roll
            bool makeSatellite = os.satelliteChance > 0f && RandomRange(0f, 1f) < os.satelliteChance;

            GameObject toInstantiate = null;
            if (makeSatellite && satellitePrefab != null)
            {
                toInstantiate = satellitePrefab;
            }
            else if (prefab != null)
            {
                toInstantiate = prefab;
            }
            else
            {
                // nothing to place, skip
                placed = true; // stop trying
                continue;
            }

        // compute final world position first
Vector3 worldPos = parent.TransformPoint(localPos);

// instantiate directly at that world position
var rawInstance = PrefabUtilitySafe.Instantiate((UnityEngine.Object)toInstantiate, worldPos, Quaternion.identity);
var go = rawInstance as GameObject;

if (go == null)
{
    Debug.LogWarning($"[OrbitGenerator] Failed to instantiate prefab for orbit '{os.name}'.");
    continue;
}
// Add collision script so asteroid can trigger effects

// now parent it, preserving world space
go.transform.SetParent(parent, true);


            // random rotation
            if (os.randomizeRotation)
                go.transform.localRotation = Quaternion.Euler(0f, (float)(_rng.NextDouble() * 360f), 0f);

            // scale according to orbit index if desired
            float sizeFactor = os.sizeMultiplier + orbitIndex * os.sizeGrowthPerOrbit;
            float randomSize = RandomRange(os.scaleRange.x, os.scaleRange.y);

            // guard scale too
            float finalScale = randomSize * sizeFactor;
            if (float.IsNaN(finalScale) || float.IsInfinity(finalScale) || finalScale <= 0f)
            {
                // clamp to sensible defaults
                finalScale = Mathf.Clamp(finalScale, 0.01f, 100f);
            }
            go.transform.localScale = Vector3.one * finalScale;

            // reset bad Rigidbody velocities (if any)
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // if any component is not finite, zero velocities
                if (!IsFinite(rb.velocity) || !IsFinite(rb.angularVelocity))
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // attach small self rotator for visual movement
            if (os.selfRotationRange != Vector2.zero)
            {
                var sr = go.GetComponent<SelfRotator>();
                if (sr == null) sr = go.AddComponent<SelfRotator>();
                sr.rotationSpeed = RandomRange(os.selfRotationRange.x, os.selfRotationRange.y);
            }

            placedPositions.Add(localPos);
            placed = true;
        } // while attempts
    } // for count
}

// --------------------- HELPER ---------------------
bool IsFinite(Vector3 v)
{
    return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
         || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
}

bool IsFinite(Vector2 v)
{
    return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y));
}

bool IsFinite(Vector3? nullable)
{
    if (nullable == null) return true;
    return IsFinite(nullable.Value);
}


    [ContextMenu("Clear Orbits")]
    public void ClearOrbits()
    {
        for (int i = _orbitParents.Count - 1; i >= 0; i--)
        {
            var go = _orbitParents[i];
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.GameObjectUtility.SetParentAndAlign(go, null);
          UnityEditor.EditorApplication.delayCall += () => { if (go) UnityEngine.Object.DestroyImmediate(go); };

#else
            if (go) DestroyImmediate(go);
#endif
        }
        _orbitParents.Clear();

        // also clear any leftover children that are still parented to this object but not tracked
        if (parentObjectsUnderThis)
        {
            var children = new List<Transform>();
            foreach (Transform t in transform) children.Add(t);
            foreach (var c in children)
            {
                if (c.name.StartsWith("Orbit_"))
                {
#if UNITY_EDITOR
                   UnityEditor.EditorApplication.delayCall += () => { if (c) UnityEngine.Object.DestroyImmediate(c.gameObject); };

#else
                    DestroyImmediate(c.gameObject);
#endif
                }
            }
        }
    }

    // helpful gizmos to preview rings
    void OnDrawGizmosSelected()
    {
        if (!blackHole) blackHole = transform;
        Vector3 center = blackHole.position;
        for (int i = 0; i < orbits.Count; i++)
        {
            var os = orbits[i];
            float r = os.useRelativeDistance && i > 0 ? (GetComputedRadius(i)) : os.radius;
            Color c = Color.Lerp(Color.yellow, Color.cyan, (float)i / Mathf.Max(1, orbits.Count - 1));
            Gizmos.color = c;
            DrawRingGizmo(center, r, os.ringWidth);
        }
    }

    float GetComputedRadius(int index)
    {
        float last = 0f;
        for (int i = 0; i <= index; i++)
        {
            var os = orbits[i];
            float r = os.radius;
            if (os.useRelativeDistance)
            {
                r = last + os.distanceFromPrevious;
            }
            r += os.radiusJitter * 0.5f;
            last = r + os.ringWidth * 0.5f + os.distanceBufferAfter;
        }
        return last;
    }

    void DrawRingGizmo(Vector3 center, float radius, float width)
    {
        const int segments = 64;
        float inner = Mathf.Max(0.001f, radius - width * 0.5f);
        float outer = radius + width * 0.5f;

        Vector3 prevInner = center + Quaternion.Euler(0, 0, 0) * Vector3.forward * inner;
        Vector3 prevOuter = center + Quaternion.Euler(0, 0, 0) * Vector3.forward * outer;

        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * 360f;
            Vector3 nextInner = center + Quaternion.Euler(0, a, 0) * Vector3.forward * inner;
            Vector3 nextOuter = center + Quaternion.Euler(0, a, 0) * Vector3.forward * outer;
            Gizmos.DrawLine(prevInner, nextInner);
            Gizmos.DrawLine(prevOuter, nextOuter);
            prevInner = nextInner; prevOuter = nextOuter;
        }
    }

    // small utility
    float RandomRange(float a, float b) => Mathf.Lerp(a, b, (float)_rng.NextDouble());

    #region Helper Components
    // rotates the parent transform to make everything child orbit
    class OrbitRotator : MonoBehaviour
    {
        public float degreesPerSecond = 10f;
        public Vector3 rotateAxis = Vector3.up;

        void Update()
        {
            
            if (Application.isPlaying) transform.Rotate(rotateAxis, degreesPerSecond * Time.deltaTime, Space.Self);

        }
    }

    // simple self rotator used for visuals
    class SelfRotator : MonoBehaviour
    {
        public float rotationSpeed = 10f;
        void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
    #endregion
}

[System.Serializable]
public class OrbitSettings
{
    public string name = "";
[Header("Vertical Variation")]
public float verticalJitter = 2f; // random Y offset range (± value)

    [Header("Radius / spacing")]
    public bool useRelativeDistance = true; // if true, radius is computed from previous orbit + distanceFromPrevious
    public float radius = 50f; // used if useRelativeDistance is false (or as base jitter)
    public float distanceFromPrevious = 40f; // used when useRelativeDistance == true
    public float radiusJitter = 2f; // small random jitter applied to radius
    public float ringWidth = 6f; // thickness of ring
    public float distanceBufferAfter = 5f; // breathing space after this ring

    [Header("Density")]
    public int density = 20;
    public bool useDensityJitter = true;
    public float densityJitter = 0.15f; // +- percentage
    public float minSpacing = 2f; // minimal distance between spawned objects on the ring

    [Header("Prefabs")]
    public GameObject[] asteroidPrefabs; // local override, falls back to generator's global list
    [Range(0f, 1f)]
    public float satelliteChance = 0.02f; // small chance to replace an asteroid with a satellite

    [Header("Visuals")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    public float sizeMultiplier = 1f; // base multiplier
    public float sizeGrowthPerOrbit = 0.08f; // how much bigger each subsequent orbit becomes
    public bool randomizeRotation = true;
    public Vector2 selfRotationRange = new Vector2(-30f, 30f); // degrees/sec for self rotation

    [Header("Motion")]
    public float orbitSpeed = 12f; // degrees/sec for this ring
    public Vector3 orbitAxis = Vector3.up;
}

/// <summary>
/// Small helper to instantiate prefabs that works both in editor and at runtime.
/// We can't directly call PrefabUtility.Instantiate for runtime builds, so this wrapper
/// decides which instantiate method to use. Keep it internal to keep the generator file
/// contained (no editor file required).
/// </summary>


static class PrefabUtilitySafe
{
    public static GameObject Instantiate(Object prefab, Vector3 pos, Quaternion rot)
    {
#if UNITY_EDITOR
        // Only use PrefabUtility if running inside the Editor
        if (!Application.isPlaying)
        {
            var go = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go != null)
            {
                go.transform.position = pos;
                go.transform.rotation = rot;
                return go;
            }
        }
#endif
        // Runtime-safe fallback
        return Object.Instantiate(prefab, pos, rot) as GameObject;
    }
}

