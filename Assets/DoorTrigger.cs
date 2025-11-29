
using System.Collections;using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public DoorAnimation door;
    public AudioSource doorClose;
    public AudioSource doorOpen;

    private bool playerInside = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            door.OpenDoor();
            doorOpen.Play();
            door.dontClose = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            door.dontClose = false;
            playerInside = false;
            StartCoroutine(CloseAfterDelay());
        }
    }

    private IEnumerator CloseAfterDelay()
    {
        // Small delay to ensure player actually left
        yield return new WaitForSeconds(0.5f);

        if (!playerInside) // Still no player after delay
        {
            door.dontClose = false;
            door.CloseDoor();
            doorClose.Play();
        }
    }
}
