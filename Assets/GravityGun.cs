
using System.Collections.Generic;
using UnityEngine;

using System.Collections;  
public class GravityGun : MonoBehaviour
{
    public Transform cam;
    public float lcRange = 3f;
    public bool isGravitied; 
    public bool isOnGravifiedSurface;
        public Vector3 gravityDir = Vector3.down;   // default gravity direction
    public Vector3 localGravityDir = Vector3.down;
    public float gravityStrength = 9.81f;
    public void Update()
   {
        Vector3 velocity = gravityDir * gravityStrength * Time.deltaTime;

    GetComponent<CharacterController>().Move(velocity);
    if(Input.GetMouseButtonDown(0))
    {
       Ray ray = new Ray(cam.position, cam.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, lcRange))
            {
                if (hit.collider.gameObject.CompareTag("Asteroid"))

                {
                StartCoroutine(MoveTowardsHit(hit.point, hit.collider.transform));
                
                
                
                }
            }
    }
    
void OnControllerColliderHit(ControllerColliderHit hit)
{
    if (hit.collider.CompareTag("Gravified"))
    {
 
        localGravityDir = (hit.collider.transform.position - transform.position).normalized;
    }
}
   }
   public IEnumerator MoveTowardsHit(Vector3 hitPos, Transform t)
   {
    t.tag = "Gravified";
    Debug.Log("Gravified " + t.name);
    float time = 0f;
   
    while(time < 0.5f)
    {
         time += Time.deltaTime;     
        yield return null;
    }
    t.tag = "Asteroid";
   }
   void OnCollisionEnter(Collision other)
   {
    if(other.gameObject.tag == "Gravified")
    {
        isGravitied = true;
    }
   }
}
