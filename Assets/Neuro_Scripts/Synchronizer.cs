using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Synchronizer : MonoBehaviour
{
    public static Synchronizer Instance;
    [SerializeField] bool debugMode = false;

    const string ADDR = "127.0.0.1";
    const int PORT = 12345;

    TcpClient client;
    NetworkStream stream;

    void Awake()
    {
        // Load as singleton
        if (Synchronizer.Instance != null && Synchronizer.Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Synchronizer.Instance = this;

        // Connect to sync server
        if (!debugMode)
        {
            client = new TcpClient(ADDR, PORT);
            stream = client.GetStream();
        }
    }

    //void Start()
    //{
    //    DataManager.Instance.LogEvent("Connected Sync Server");
    //}

    public async void SendTrigger()
    {
        if (!debugMode) 
        { 
            byte[] data = Encoding.UTF8.GetBytes("TRIG");
            await stream.WriteAsync(data, 0, 4);
        }
    }

    private void Cleanup()
    {
        if (!debugMode)
        {
            client.Close();
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    void OnApplicationQuit()
    {
        Cleanup();
    }
}
