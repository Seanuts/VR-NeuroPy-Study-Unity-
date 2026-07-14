using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Synchronizer : MonoBehaviour
{
    public static Synchronizer Instance;

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
        DontDestroyOnLoad(this.gameObject);

        // Connect to sync server
        client = new TcpClient(ADDR, PORT);
        stream = client.GetStream();
    }

    void Start()
    {
        DataManager.Instance.LogEvent("Connected Sync Server");
    }

    private void OnApplicationQuit()
    {
        client.Close();
    }

    public async void SendTrigger()
    {
        byte[] data = Encoding.UTF8.GetBytes("TRIG");
        await stream.WriteAsync(data, 0, 4);
    }
}
