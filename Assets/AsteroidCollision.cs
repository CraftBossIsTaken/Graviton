using UnityEngine;

public class AsteroidCollision : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("ship"))
        {
            HitShip();
             Debug.Log("HitShip called!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ship"))
        {
            HitShip();
             Debug.Log("HitShip called!");
        }
    }

void HitShips()
{
     Debug.Log("HitShip called!");
    int damage = Random.Range(5,15);
    Debug.Log("Damage to apply: " + damage);
    
    if (GameManager.Instance == null)
    {
        Debug.LogError("GameManager.Instance is null!");
        return;
    }

    GameManager.Instance.DealDamage(damage);
    Debug.Log("New hull integrity: " + GameManager.Instance.hullIntegrity);
    try
    {
        GravitationalWaveManager.Instance.StartWarningBlink(5f);
        
    }
    catch (System.Exception e)
    {
        Debug.LogError("GWM error: " + e);
    }

    try
    {
        ImpactSpinController.Instance.StartImpact();
    }
    catch (System.Exception e)
    {
        Debug.LogError("ImpactSpinController error: " + e);
    }

   
}
void HitShip()
{
    Debug.Log("HitShip START");
    // temporarily comment out StartImpact / StartWarningBlink
}

}
