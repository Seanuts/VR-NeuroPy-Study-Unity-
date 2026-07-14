using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    // Filesystem
    string folderPath;
    StreamWriter leftEyeStream;
    StreamWriter rightEyeStream;
    StreamWriter eventLogStream;

    // Clock
    Stopwatch clock;

    void Awake()
    {
        // Load as singleton
        if( DataManager.Instance != null && DataManager.Instance != this )
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Initialize the clock
        clock = new Stopwatch();
        clock.Start();

        // Create unique directory for current playthrough
        string studyDataPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "data");
        folderPath = Path.Combine(studyDataPath, DateTime.Now.ToString("yyyy-MM-dd-HH-MM-ss"));
        if (! Directory.Exists(folderPath) )
        {
            Directory.CreateDirectory(folderPath);
        }

        // Init eyetracking and event logging files
        leftEyeStream = new StreamWriter(Path.Combine(folderPath, "left_eye_data.csv"), false);
        leftEyeStream.WriteLine("Timestamp,Position,Rotation");
        
        rightEyeStream = new StreamWriter(Path.Combine(folderPath, "right_eye_data.csv"), false);
        rightEyeStream.WriteLine("Timestamp,Position,Rotation");
        
        eventLogStream = new StreamWriter(Path.Combine(folderPath, "event_log.csv"), false);
        eventLogStream.WriteLine("Timestamp,Event");
        LogEvent("Finished Init");

    }

    public void LogEvent(string eventName)
    {
        eventLogStream.WriteLine(clock.ElapsedMilliseconds + "," + eventName);
        Synchronizer.Instance.SendTrigger(); // also send a trigger to BioPaC
    }

    public void LogEyeDataLeft(Vector3 position, Quaternion rotation)
    {
        string data = position.ToString() + "," + rotation.ToString();
        leftEyeStream.WriteLine(clock.ElapsedMilliseconds + "," + data);
    }

    public void LogEyeDataRight(Vector3 position, Quaternion rotation)
    {
        string data = position.ToString() + "," + rotation.ToString();
        rightEyeStream.WriteLine(clock.ElapsedMilliseconds + "," + data);
    }

    public void LogResultData(ModeData vr, ModeData hybrid, ModeData desktop)
    {
        string content = "VR:\n" + vr.ToString() + "\n\n";
        content += "Hybrid:\n" + hybrid.ToString() + "\n\n";
        content += "Desktop:\n" + desktop.ToString() + "\n\n";
        File.WriteAllText(Path.Combine(folderPath, "results.txt"), content);
    }

    // For cleanup
    void OnApplicationQuit()
    {
        if (leftEyeStream != null)
        {
            leftEyeStream.Flush();
            leftEyeStream.Close();
        }
        if (rightEyeStream != null)
        {
            rightEyeStream.Flush();
            rightEyeStream.Close();
        }
        if (eventLogStream != null)
        {
            eventLogStream.Flush();
            eventLogStream.Close();
        }
    }
}
