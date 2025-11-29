
using UnityEngine;

public class CollisionDebug : MonoBehaviour
{
    [Header("Asteroid impact -> Ship break settings")]
    [Tooltip("Tag used for ship static components to compute closest impact point")]
    public string shipComponentsTag = "shipComponents";

    [Tooltip("Damage multiplier to pass into BreakAtPoint (visual intensity)")]
    public float visualImpactMultiplier = 1f;

    [Tooltip("How far back to offset decal from surface to avoid z-fighting")]
    public float decalOffset = 0.02f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Asteroid")) return;

        // Grab the asteroid position (we'll estimate contact using nearest ship component)
        Vector3 asteroidPos = other.transform.position;

        // 1) Find nearest ship component (tagged) and compute closest point on its collider
        GameObject[] candidates = GameObject.FindGameObjectsWithTag(shipComponentsTag);
        if (candidates == null || candidates.Length == 0)
        {
            Debug.LogWarning("No ship components found with tag: " + shipComponentsTag);
            // fallback: use asteroid position and ship root position as impact
            Vector3 fallbackNormal = (transform.position - asteroidPos).normalized;
        //    ShipBreakManager.Instance?.BreakAtPoint(asteroidPos, 25);
        }
        else
        {
            float bestDist = float.MaxValue;
            Vector3 bestPoint = asteroidPos;
            Vector3 bestNormal = Vector3.forward;

            foreach (var go in candidates)
            {
                if (go == null) continue;
                Collider c = go.GetComponent<Collider>();
                if (c == null) continue;

                // Closest point on this collider to the asteroid
                Vector3 p = c.ClosestPoint(asteroidPos);
                float d = Vector3.Distance(p, asteroidPos);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoint = p;
                    // normal: from asteroid into the surface
                    bestNormal = (bestPoint - asteroidPos).normalized;
                    if (bestNormal == Vector3.zero) bestNormal = transform.forward;
                }
            }

            // Slightly offset the hit point outwards to spawn decals cleanly
            Vector3 spawnPoint = bestPoint + bestNormal * decalOffset;

            // Explosion/visual intensity scaled by asteroid mass (if it has one) or randomized
            float intensity = visualImpactMultiplier;
            var aRb = other.attachedRigidbody;
            if (aRb != null) intensity *= Mathf.Clamp01(aRb.mass / 10f) + 0.5f;


            // Break ship at point
         //   if (ShipBreakManager.Instance != null)
              //  ShipBreakManager.Instance.BreakAtPoint(spawnPoint, 25);
            //else
                //Debug.LogWarning("ShipBreakManager not found in scene.");

            // Apply damage to GameManager (keep your existing functionality)
            int damage = Random.Range(5, 15);

            if (GameManager.Instance != null) GameManager.Instance.DealDamage(damage);
                Destroy(other.gameObject);
                 if (GravitationalWaveManager.Instance != null) GravitationalWaveManager.Instance.StartWarningBlink(5f);
        if(!GameManager.Instance.interact.isinBounds) return;

        
            if (ImpactSpinController.Instance != null) ImpactSpinController.Instance.StartImpact();
        
        
        }
    }
}
