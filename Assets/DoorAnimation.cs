using System.Collections;
using UnityEngine;

public class DoorAnimation : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("How far the door moves when opening (on Z axis).")]
    public float openDistance = 2f;

    [Tooltip("How fast the door moves.")]
    public float openSpeed = 2f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    public bool isAnimating = false;
    public bool isOpen = false;
    public bool dontClose = false;
    public bool isXAxis;

    void Start()
    {
        closedPosition = transform.localPosition;
        if(!isXAxis)
        {
openPosition = closedPosition + new Vector3(0, 0, openDistance);
        }
        else
        {
            openPosition = closedPosition + new Vector3(openDistance, 0, 0);
        }
        
    }

    public void OpenDoor()
    {
        if (!isAnimating && !isOpen)
        {
            StartCoroutine(MoveDoor(openPosition));
            Debug.Log("Moving to " + openPosition);
        }
    }

    public void CloseDoor()
    {
        if(dontClose) return;
     StopAllCoroutines();
            StartCoroutine(MoveDoor(closedPosition));
    
    }

    private IEnumerator MoveDoor(Vector3 targetPos)
    {

        Debug.Log("moving");
        isAnimating = true;

        Vector3 startPos = transform.localPosition;
        float time = 0f;

while (time < 1f)
{
    time += Time.deltaTime * openSpeed;
    transform.localPosition = Vector3.Lerp(startPos, targetPos, time);
 
    yield return null;
}


        transform.localPosition = targetPos;

        isOpen = (targetPos == openPosition);
        isAnimating = false;
    }
}
