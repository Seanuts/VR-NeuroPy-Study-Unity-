using System.IO;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyLogic : MonoBehaviour
{
    [SerializeField] TMP_InputField pidInput;
    [SerializeField] TextMeshProUGUI errorText;
    
    string studyDataPath;
    
    private void Awake()
    {
        // Ensure there is a data folder for study
        studyDataPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "data");
        if (!Directory.Exists(studyDataPath))
        {
            Directory.CreateDirectory(studyDataPath);
        }
    }

    public async void StartStudy()
    {
        // Verify that valid participant ID is entered
        string pidStr = pidInput.text.Trim();
        UnityEngine.Debug.Log(pidStr);

        // 1. Verify that the input is not empty
        if (pidStr == "")
        {
            errorText.text = "Please enter an ID!";
            return;
        }
        // 2. Verify that it is an integer and >= 0
        if (!int.TryParse(pidStr, out int pid) || pid < 0)
        {
            UnityEngine.Debug.Log(pid);
            UnityEngine.Debug.Log(int.TryParse(pidStr, out _));
            UnityEngine.Debug.Log(pid < 0);

            errorText.text = "ID must be a number!";
            return;
        }
        DataManager.pid = pid;
        await SceneManager.LoadSceneAsync("MainScene");
    }

    public void OpenDataFolder()
    {
        string path = studyDataPath.Replace("/", "\\");
        Process.Start("explorer.exe", path);
    }

    public async void ReturnToLobby()
    {
        await SceneManager.LoadSceneAsync("Lobby");
    }
}
