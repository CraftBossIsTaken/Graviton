using UnityEngine;
public class OrbitTrigger : MonoBehaviour
{
    public float orbitRadius;
    public Vector3 axis;

    void OnTriggerEnter(Collider other)
    {
        var trap = other.GetComponent<OrbitTrap>();
        if (trap != null)
        {
            trap.EnterOrbit(transform, axis, orbitRadius);
        }
    }
}
