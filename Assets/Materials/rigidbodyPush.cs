using UnityEngine;

public class rigidbodyPush : MonoBehaviour
{
    [Header("Push Settings")]
    public string pushTag = "Pushable"; // Tag for pushable objects
    public float pushForce = 5f;        // Force applied to objects
    public float maxPushDistance = 1.5f; // How close player must be to push

    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Only push objects with the correct tag
        if (!hit.collider.CompareTag(pushTag))
            return;

        Rigidbody rb = hit.collider.attachedRigidbody;

        // Skip if no Rigidbody or it's kinematic
        if (rb == null || rb.isKinematic)
            return;

        // Calculate push direction (horizontal only)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Apply force only if within push range
        if (pushDir.magnitude > 0.1f && Vector3.Distance(transform.position, hit.point) <= maxPushDistance)
        {
            rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
        }
    }
}
