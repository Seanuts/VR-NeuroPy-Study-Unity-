using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public class StudyCoordinator : MonoBehaviour{ //makes sure only ONE master script exists at a time

    public static StudyCoordinator Instance;

    [Header("Drag Scene Files Here")]
#if UNITY_EDITOR
    public UnityEditor.SceneAsset scene1_VR;
    public UnityEditor.SceneAsset scene2_VR_NonInteractive;
    public UnityEditor.SceneAsset scene3_Desktop;
    public UnityEditor.SceneAsset intermissionScene;
#endif

    [HideInInspector] public string nameVR, nameVRNonInt, nameDesktop, nameIntermission;

    [Header("Study Settings")]
    public float breakDurationSeconds = 120f; // 2 minutes
    private List<string> sceneQueue = new List<string>();

#if UNITY_EDITOR
    private void OnValidate(){
        // Extract typo-free names from the dragged files
        if (scene1_VR != null) nameVR = scene1_VR.name;
        if (scene2_VR_NonInteractive != null) nameVRNonInt = scene2_VR_NonInteractive.name;
        if (scene3_Desktop != null) nameDesktop = scene3_Desktop.name;
        if (intermissionScene != null) nameIntermission = intermissionScene.name;
    }
#endif

    void Awake(){
        // Keep this manager alive across all scenes
        if (Instance == null){
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStudy();
        }
        else{
            Destroy(gameObject);
        }
    }

    void InitializeStudy(){
        sceneQueue.Add(nameVR);
        sceneQueue.Add(nameVRNonInt);
        sceneQueue.Add(nameDesktop);

        for (int i = 0; i < sceneQueue.Count; i++){
            string temp = sceneQueue[i];
            int randomIndex = Random.Range(i, sceneQueue.Count);
            sceneQueue[i] = sceneQueue[randomIndex];
            sceneQueue[randomIndex] = temp;
        }

        Debug.Log("Study Order Generated: " + sceneQueue[0] + " -> " + sceneQueue[1] + " -> " + sceneQueue[2]);
    }


    public void OnPhaseComplete(){// Individual ScreenTimers will call this when they hit 00:00
        if (sceneQueue.Count > 0){
            StartCoroutine(HandleBreakAndTransition());
        }
        else{
            Debug.Log("Entire Study Complete!");
        }
    }

    IEnumerator HandleBreakAndTransition()
    {
        StopVR();// 1. Turn OFF VR so the break scene is forced to the Desktop monitor

        SceneManager.LoadScene(nameIntermission); // 2. Load the Intermission Scene
        yield return new WaitForSeconds(breakDurationSeconds); // 3. Wait for the 2 minute delay
        string nextScene = sceneQueue[0];// 4. Get the next random scene from the list
        sceneQueue.RemoveAt(0); // Remove it so it doesn't play again

        // 5. If the next scene is a VR scene, turn the headset back ON
        if (nextScene == nameVR || nextScene == nameVRNonInt){
            yield return StartCoroutine(StartVR());
        }
        // 6. Finally, load the actual study scene
        SceneManager.LoadScene(nextScene);
    }

    void StopVR(){
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager.isInitializationComplete){
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
        }
    }

    IEnumerator StartVR(){
        if (XRGeneralSettings.Instance != null && !XRGeneralSettings.Instance.Manager.isInitializationComplete){
            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
            if (XRGeneralSettings.Instance.Manager.activeLoader != null){
                XRGeneralSettings.Instance.Manager.StartSubsystems();
            }
        }
    }
}