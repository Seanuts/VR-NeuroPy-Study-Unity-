using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
//this one won't kick
public class StudyCoordinatorV2 : MonoBehaviour {
    public static StudyCoordinatorV2 Instance;

    [Header("Debug Settings")]
    [Tooltip("If checked, the study won't switch scenes or turn off VR when a phase ends.")]
    public bool debugMode = false; 

    [Header("Drag Scene Files Here")]
#if UNITY_EDITOR
    public UnityEditor.SceneAsset scene1_VR;
    public UnityEditor.SceneAsset scene2_VR_NonInteractive;
    public UnityEditor.SceneAsset scene3_Desktop;
    public UnityEditor.SceneAsset intermissionScene;
#endif

    [HideInInspector] public string nameVR, nameVRNonInt, nameDesktop, nameIntermission;

    [Header("Study Settings")]
    public float breakDurationSeconds = 120f; 
    private List<string> sceneQueue = new List<string>();

#if UNITY_EDITOR
    private void OnValidate() {
        if (scene1_VR != null) nameVR = scene1_VR.name;
        if (scene2_VR_NonInteractive != null) nameVRNonInt = scene2_VR_NonInteractive.name;
        if (scene3_Desktop != null) nameDesktop = scene3_Desktop.name;
        if (intermissionScene != null) nameIntermission = intermissionScene.name;
    }
#endif

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStudy();
        } else {
            Destroy(gameObject);
        }
    }

    void InitializeStudy() {
        sceneQueue.Add(nameVR);
        sceneQueue.Add(nameVRNonInt);
        sceneQueue.Add(nameDesktop);

        for (int i = 0; i < sceneQueue.Count; i++) {
            string temp = sceneQueue[i];
            int randomIndex = Random.Range(i, sceneQueue.Count);
            sceneQueue[i] = sceneQueue[randomIndex];
            sceneQueue[randomIndex] = temp;
        }
    }

    public void OnPhaseComplete() {
        // --- THE FIX IS HERE ---
        if (debugMode) {
            Debug.Log("<color=cyan>Debug Mode Active:</color> Phase completed, but transition blocked.");
            return; // Stops the function right here!
        }

        if (sceneQueue.Count > 0) {
            StartCoroutine(HandleBreakAndTransition());
        }
    }

    IEnumerator HandleBreakAndTransition() {
        StopVR();
        SceneManager.LoadScene(nameIntermission);
        yield return new WaitForSeconds(breakDurationSeconds);

        string nextScene = sceneQueue[0];
        sceneQueue.RemoveAt(0);

        if (nextScene == nameVR || nextScene == nameVRNonInt) {
            yield return StartCoroutine(StartVR());
        }

        SceneManager.LoadScene(nextScene);
    }

    void StopVR() {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager.isInitializationComplete) {
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
        }
    }

    IEnumerator StartVR() {
        if (XRGeneralSettings.Instance != null && !XRGeneralSettings.Instance.Manager.isInitializationComplete) {
            yield return XRGeneralSettings.Instance.Manager.InitializeLoader();
            if (XRGeneralSettings.Instance.Manager.activeLoader != null) {
                XRGeneralSettings.Instance.Manager.StartSubsystems();
            }
        }
    }
}