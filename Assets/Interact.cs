using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering;
using StarterAssets;
using UnityEngine.InputSystem;

public class Interact : MonoBehaviour
{
    public bool isinBounds;
    public Transform cam;
    public Transform cam2;
    public DematerializerTargeting t;
    public FirstPersonController c;
    [Header("Global Volume Switching")]
    public Volume normalVolume;
    public Volume turretVolume;
    private bool inTurretMode = false;
    public GameObject NormalUI;
    public GameObject DMUI;
    public CinemachineBrain b;
    public CameraControls cc;
    public CinemachineVirtualCamera chairCam;
       public CinemachineVirtualCamera mainCam;
    [Header("First-time turret rotation")]
    [Tooltip("Duration for the smooth rotation to 0,0,0 when turret is first enabled.")]
    public float firstRotateDuration = 0.6f;
public InputActionReference interact;

    // internal flag to ensure we only smoothly rotate to zero once
    private bool rotatedToZero = false;
void OnEnable()
{
    interact.action.Enable();
}

void OnDisable()
{
    interact.action.Disable();
}

    void Update()
    {
      if (interact.action.WasPerformedThisFrame())
        {
            if (inTurretMode)
            {
                ReturnToNormalCamera(0.2f);
                return;
                 cam2.GetComponent<CinemachineVirtualCamera>().Priority = 0;
            }
            if(cc.isSteering)
            {
                ExitSteerMode();
            }

            Ray ray = new Ray(cam.position, cam.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 3))
            {
                if (hit.collider.gameObject.CompareTag("turretMode"))
                {
                    CinemachineVirtualCamera vcam = cam.GetComponent<CinemachineVirtualCamera>();
                    if (vcam != null)
                    {
                        // Toggle aim mode
                        t.isAiming = !t.isAiming;

                       
                        bool willEnable = !vcam.enabled;
                        vcam.enabled = willEnable;

                        // Swap volume weights instantly
                        if (!inTurretMode)
                        {
                            if (normalVolume != null && turretVolume != null)
                            {
                                normalVolume.weight = 0f;
                                cam2.GetComponent<CinemachineVirtualCamera>().Priority = 30;
                                turretVolume.weight = 1f;
                            }
                            inTurretMode = true;
                            c.enabled = false;
                            NormalUI.SetActive(false);
                            DMUI.SetActive(true);

                            // If this is the very first time entering turret mode, smoothly rotate main cam to 0,0,0
                            if (!rotatedToZero)
                            {
                                // Start coroutine that will temporarily disable the vcam, rotate the main camera,
                                // set the vcam transform to match, then re-enable the vcam.
                                StartCoroutine(SmoothRotateMainCamToZeroThenEnableVcam(vcam, firstRotateDuration));
                            }
                            else
                            {
                                // Normal behavior after first-time rotation: immediately match vcam transform to cam2
                                vcam.transform.eulerAngles = cam2.eulerAngles;
                            }
                        }
                        else
                        {
                            if (normalVolume != null && turretVolume != null)
                            {
                                normalVolume.weight = 1f;
                                turretVolume.weight = 0f;
                                   cam2.GetComponent<CinemachineVirtualCamera>().Priority = 0;
                            }
                            inTurretMode = false;
                        }
                    }
                }

                if (hit.collider.gameObject.CompareTag("ShipControl"))
                {
                    Debug.Log("meow1");
                    ShipModifier sm = hit.collider.GetComponent<ShipModifier>();
                    if (sm != null)
                    {
                        bool success = GameManager.Instance.TryModifyAlloc(sm.modifierName, sm.modifierChange);

                        if (success)
                            Debug.Log($"{sm.modifierName} modified successfully!");
                        else
                            Debug.Log("Change rejected (would exceed 100 or invalid system)");
                    }
                    else
                    {
                        Debug.Log(hit.collider.gameObject.transform.name + " is tagged shipcontrol but doesn't have the script go fix it stupid");
                    }
                }
                if(hit.collider.gameObject.CompareTag("steer"))
                {
                    if(!cc.isSteering)
                    {
                      EnterSteerMode();
                    }
                    else
                    {
                        
                    }
                }
            }
        }
    }
private void EnterSteerMode()
{
    cc.isSteering = true;

    // Smooth blend using priority:
    chairCam.Priority = 20;
    mainCam.Priority = 1;
    c.enabled = false;
    // First-time rotation logic still works the same

}

private void ExitSteerMode()
{
    cc.isSteering = false;

    // Restore priority to blend back
    mainCam.Priority = 20;
    chairCam.Priority = 1;
c.enabled = true;
}

    // Public method to restore normal camera and volumes
    public void ReturnToNormalCamera(float delay)
    {
        StartCoroutine(RTNC(delay));
    }

    public IEnumerator RTNC(float delay)
    {
        yield return new WaitForSeconds(delay);
        CinemachineVirtualCamera vcam = cam.GetComponent<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            vcam.enabled = true;
            t.isAiming = false;
        }

        if (normalVolume != null && turretVolume != null)
        {
            normalVolume.weight = 1f;
             cam2.GetComponent<CinemachineVirtualCamera>().Priority = 0;
            turretVolume.weight = 0f;
        }
        c.enabled = true;
        inTurretMode = false;
        NormalUI.SetActive(true);
        DMUI.SetActive(false);
    }

    /// <summary>
    /// Smoothly rotates the main camera (cam) to 0,0,0 over duration, while temporarily disabling the provided vcam.
    /// After rotation completes, the vcam's transform is set to the new rotation and the vcam is re-enabled.
    /// This ensures Cinemachine won't immediately override our smooth rotation and guarantees the vcam starts aligned.
    /// </summary>
    private IEnumerator SmoothRotateMainCamToZeroThenEnableVcam(CinemachineVirtualCamera vcam, float duration)
    {
        if (cam == null || vcam == null)
        {
            yield break;
        }

        // remember previous enabled state
        bool prevEnabled = vcam.enabled;
        // make sure vcam is disabled so it won't overwrite our transform during the smooth rotation
        vcam.enabled = false;

        Quaternion startRotation = cam.rotation;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, 0f);

        if (duration <= 0f)
        {
            cam.rotation = targetRotation;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // use smoothstep for a nicer feel
                t = t * t * (3f - 2f * t);
                cam.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            cam.rotation = targetRotation;
        }

        // Make sure the vcam transform matches the camera now, so re-enabling won't snap away.
        vcam.transform.rotation = cam.rotation;

        // mark that we've done the initial rotation so this only happens once
        rotatedToZero = true;

        // restore vcam enabled state (likely true because we toggled it earlier)
        vcam.enabled = prevEnabled;
    }
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("shipBounds"))
        {
            isinBounds = true;
        }
    }
       void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("shipBounds"))
        {
            isinBounds = false;
        }
    }
}
