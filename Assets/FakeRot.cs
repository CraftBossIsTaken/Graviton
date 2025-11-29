using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FakeRot : MonoBehaviour
{
   public float speed = 5f;
    public Transform bhCenter;
    void Update()
    {
        transform.Rotate(Vector3.up * speed);
    }
}
