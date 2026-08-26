using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    const string FACE_FILE = "face_data.csv";
    const string LEFT_EYE_FILE = "left_eye_data.csv";
    const string RIGHT_EYE_FILE = "right_eye_data.csv";
    const string EVENT_LOG_FILE = "event_log.csv";
    const string RESULTS_FILE = "results.csv";

    public static DataManager Instance;
    public static int pid;

    // Filesystem
    string folderPath;
    StreamWriter faceStream;
    StreamWriter leftEyeStream;
    StreamWriter rightEyeStream;
    StreamWriter eventLogStream;
    StreamWriter resultStream;

    // System
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

        // Initialize the clock
        clock = new Stopwatch();
        clock.Start();

        // Create unique directory for current playthrough
        string studyDataPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "data");
        string folderName = $"p{pid}_{DateTime.Now.ToString("yyyy-MM-dd-HH_mm_ss")}";
        folderPath = Path.Combine(studyDataPath, folderName);
        if ( !Directory.Exists(folderPath) )
        {
            Directory.CreateDirectory(folderPath);
        }

        // Init eye/face tracking and event logging files
        // TODO: fix the extra 2 headers
        string[] allNames = System.Enum.GetNames(typeof(OVRFaceExpressions.FaceExpression));
        string[] faceVectorNames = allNames.Take(allNames.Length - 2).ToArray();
        faceStream = new StreamWriter(Path.Combine(folderPath, FACE_FILE), false);
        faceStream.WriteLine("Timestamp," + string.Join(",", faceVectorNames));

        leftEyeStream = new StreamWriter(Path.Combine(folderPath, LEFT_EYE_FILE), false);
        leftEyeStream.WriteLine("Timestamp,PosX,PosY,PosZ,RotW,RotX,RotY,RotZ");
        
        rightEyeStream = new StreamWriter(Path.Combine(folderPath, RIGHT_EYE_FILE), false);
        rightEyeStream.WriteLine("Timestamp,PosX,PosY,PosZ,RotW,RotX,RotY,RotZ");
        
        eventLogStream = new StreamWriter(Path.Combine(folderPath, EVENT_LOG_FILE), false);
        eventLogStream.WriteLine("Timestamp,Event");

        resultStream = new StreamWriter(Path.Combine(folderPath, RESULTS_FILE), false);
        resultStream.WriteLine("Question,Result");
        LogEvent("Finished initialization");

    }

    public void LogEvent(string eventName)
    {
        if (eventLogStream == null) return;
        eventLogStream.WriteLine(clock.ElapsedMilliseconds + "," + eventName);
        Synchronizer.Instance.SendTrigger(); // also send a trigger to BioPaC
    }    
    
    public void LogFaceData(float[] weights)
    {
        if (faceStream == null) return;
        string data = string.Join(",", Array.ConvertAll(weights, x => x.ToString()));
        faceStream.WriteLine(clock.ElapsedMilliseconds + "," + data);
    }

    public void LogEyeDataLeft(Vector3 pos, Quaternion rot)
    {
        if (leftEyeStream == null) return;
        string data = $"{pos.x},{pos.y},{pos.z},{rot.w},{rot.x},{rot.y},{rot.z}";
        leftEyeStream.WriteLine(clock.ElapsedMilliseconds + "," + data);
    }

    public void LogEyeDataRight(Vector3 pos, Quaternion rot)
    {
        if (rightEyeStream == null) return;
        string data = $"{pos.x},{pos.y},{pos.z},{rot.w},{rot.x},{rot.y},{rot.z}";
        rightEyeStream.WriteLine(clock.ElapsedMilliseconds + "," + data);
    }

    public void LogResultData(string question, string result)
    {
        if (resultStream == null) return;
        resultStream.WriteLine($"{question},{result}");
    }

    // For cleanup
    void Cleanup()
    {
        if (resultStream != null)
        {
            resultStream.Flush();
            resultStream.Close();
            resultStream = null;
        }
        if (faceStream != null)
        {
            faceStream.Flush();
            faceStream.Close();
            faceStream = null;
        }
        if (leftEyeStream != null)
        {
            leftEyeStream.Flush();
            leftEyeStream.Close();
            leftEyeStream = null;
        }
        if (rightEyeStream != null)
        {
            rightEyeStream.Flush();
            rightEyeStream.Close();
            rightEyeStream = null;
        }
        if (eventLogStream != null)
        {
            eventLogStream.Flush();
            eventLogStream.Close();
            eventLogStream = null;
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void OnApplicationQuit()
    {
        Cleanup();
    }
}
