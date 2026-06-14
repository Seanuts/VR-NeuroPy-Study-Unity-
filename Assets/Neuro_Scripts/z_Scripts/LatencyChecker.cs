using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

public class LatencyChecker : MonoBehaviour
{
    private UdpClient udpClient;
    private string pythonIP = "127.0.0.1";
    private int pythonPort = 8000;

    void Start()
    {
        udpClient = new UdpClient();
    }

    public async void TriggerLatencyCheck()
    {
        UnityEngine.Debug.Log("BUTTON WAS CLICKED! Attempting to send Ping...");
        Stopwatch stopwatch = new Stopwatch();
        byte[] pingData = Encoding.UTF8.GetBytes("PING");

        try
        {
            stopwatch.Start();
            
            // 1. Send Ping to Python
            await udpClient.SendAsync(pingData, pingData.Length, pythonIP, pythonPort);

            // 2. Wait for Pong from Python
            Task<UdpReceiveResult> receiveTask = udpClient.ReceiveAsync();
            
            // Wait up to 3 seconds total for the whole process
            if (await Task.WhenAny(receiveTask, Task.Delay(3000)) == receiveTask)
            {
                stopwatch.Stop();
                string response = Encoding.UTF8.GetString(receiveTask.Result.Buffer);
                double totalSeconds = stopwatch.Elapsed.TotalSeconds;

                if (response == "PONG")
                {
                    UnityEngine.Debug.Log($"<color=green>[Latency] FULL Round-Trip (Unity-Py-C++): {totalSeconds:F3} seconds</color>");
                }
                else if (response == "PONG_NO_CPP")
                {
                    // Subtracting the known 0.5s timeout gives a rough estimate of the Unity <-> Py latency
                    double adjustedTime = totalSeconds - 0.5; 
                    UnityEngine.Debug.LogWarning($"<color=yellow>[Latency] PARTIAL Trip (C++ offline). Unity <-> Py latency roughly: {adjustedTime:F3} seconds</color>");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Latency] Received unexpected response: " + response);
                }
            }
            else
            {
                stopwatch.Stop();
                UnityEngine.Debug.LogError("[Latency] Critical Timeout. Python script is likely not running.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[Latency Check] Network Error: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (udpClient != null) udpClient.Close();
    }
}