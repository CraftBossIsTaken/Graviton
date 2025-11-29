using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using UnityEngine.SceneManagement;
public class UIManager : MonoBehaviour
{
    public GameObject settings;
    public GameObject pause;
    public GameObject overlay;
    public bool isPaused = false;
    public FirstPersonController fp;
    // Start is called before the first frame update
    void Start()
    {
      
    }
    public void EnableFP()
    {
         isPaused = false; 
         fp.enabled = true;
    }

    // Update is called once per frame
    void Update()
    {
        
        if(isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 0f;
            fp.enabled = false;
        }
        else
        {
            Time.timeScale = 1f;
        }
        if(Input.GetKeyDown(KeyCode.Escape))
        {
           if (settings.activeSelf)
    {
        settings.SetActive(false);
        pause.SetActive(true);
        overlay.SetActive(false);
        return;
    }

    if (pause.activeSelf)
    {
        pause.SetActive(false);
        overlay.SetActive(true);
        isPaused = false;
         fp.enabled = true;
        return;
    }
    if (!isPaused)
    {
        overlay.SetActive(false);
        pause.SetActive(true);
        settings.SetActive(false);
        isPaused = true;
    }
        }
    }
    public void ToggleMenu(GameObject menu)
    {
        menu.SetActive(true);
    }
    public void DisableMenus(bool withOverlay)
    {
        settings.SetActive(false);
        pause.SetActive(false);
        if(withOverlay){
        overlay.SetActive(true);}else{overlay.SetActive(false);}
    }
    public void BackToMenu()
    {
        SceneManager.LoadScene(0);
    }
}
