using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class AutoSaveOnPlay
{
    // // Static constructor to initialize when script loads
    // static AutoSaveOnPlay()
    // {
    //     // Subscribe to the play mode state change event
    //     EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    // }

    // private static void OnPlayModeStateChanged(PlayModeStateChange state)
    // {
    //     // Check if the editor is about to enter Play mode
    //     if (state == PlayModeStateChange.ExitingEditMode)
    //     {
    //         Debug.Log("Auto-saving scene before entering Play mode...");

    //         // Save all open scenes
    //         if (EditorSceneManager.SaveOpenScenes())
    //         {
    //             Debug.Log("Scenes saved successfully.");
    //         }
    //         else
    //         {
    //             Debug.LogWarning("Failed to save scenes.");
    //         }

    //         // Save all assets in the project
    //         AssetDatabase.SaveAssets();
    //         Debug.Log("Assets saved successfully.");
    //     }
    // }
}
