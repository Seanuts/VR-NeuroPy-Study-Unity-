using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class BiometricReceiver : MonoBehaviour
{
    UdpClient client;
    Thread receiveThread;

    public float edaValue;
    public float respValue;
    public double sensorTimestamp;

    void Start()
    {
        client = new UdpClient(5005);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            byte[] data = client.Receive(ref remoteEP);
            string message = Encoding.UTF8.GetString(data);

            string[] parts = message.Split(',');

            if (parts.Length == 3)
            {
                sensorTimestamp = double.Parse(parts[0]);
                edaValue = float.Parse(parts[1]);
                respValue = float.Parse(parts[2]);
            }
        }
    }

    void Update()
    {
        Debug.Log($"EDA: {edaValue} | Resp: {respValue} | Sensor Time: {sensorTimestamp}");
    }
}