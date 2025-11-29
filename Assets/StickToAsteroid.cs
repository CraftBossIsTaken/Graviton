using UnityEngine;

public class StickToAsteroid : MonoBehaviour
{
    public CharacterController controller;
    public float groundCheckDistance = 0.2f;

    private Transform currentAsteroid;
    private Transform movingRoot;

    private Vector3 lastRootPos;
    private Quaternion lastRootRot;

    private Vector3 asteroidLinearVelocity;
    private Vector3 externalVelocity;

    void Update()
    {
        HandleAsteroidStick();
    }

    void HandleAsteroidStick()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance + 0.1f))
        {
            if (hit.collider.CompareTag("Asteroid"))
            {
                if (currentAsteroid != hit.collider.transform)
                {
                    currentAsteroid = hit.collider.transform;

                    // Find the top-most parent that actually moves
                    movingRoot = currentAsteroid;
                    while (movingRoot.parent != null)
                        movingRoot = movingRoot.parent;

                    lastRootPos = movingRoot.position;
                    lastRootRot = movingRoot.rotation;
                }

                // --- POSITION DELTA (world) ---
                Vector3 posDelta = movingRoot.position - lastRootPos;
                asteroidLinearVelocity = posDelta / Time.deltaTime;

                // --- ROTATION DELTA (world) ---
                Quaternion rotDelta = movingRoot.rotation * Quaternion.Inverse(lastRootRot);

                // compute how much rotation moves the player relative to orbit center
                Vector3 relativePos = transform.position - movingRoot.position;
                Vector3 rotatedPos = rotDelta * relativePos;
                Vector3 rotationMovement = rotatedPos - relativePos;

                // --- APPLY TOTAL MOVEMENT ---
                Vector3 finalDelta = posDelta + rotationMovement;

                if (finalDelta != Vector3.zero)
                    controller.Move(finalDelta);

                // save for next frame
                lastRootPos = movingRoot.position;
                lastRootRot = movingRoot.rotation;

                return;
            }
        }

        // Not standing on asteroid anymore
        if (currentAsteroid != null)
        {
            externalVelocity = asteroidLinearVelocity;
        }

        currentAsteroid = null;
    }

    public Vector3 ConsumeExternalVelocity()
    {
        Vector3 v = externalVelocity;
        externalVelocity = Vector3.zero;
        return v;
    }
}
