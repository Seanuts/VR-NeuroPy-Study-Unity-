using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using UnityEngine.UI; // DEBUG

public class EventCoordinator : MonoBehaviour
{
    const string ADDR = "127.0.0.1";
    const int PORT = 12345;

    TcpClient client;
    NetworkStream stream;


    void Start()
    {
        client = new TcpClient(ADDR, PORT);
        stream = client.GetStream();
    }


    private void OnApplicationQuit()
    {
        client.Close();
    }


    public async void SendTrigger()
    {
        byte[] data = Encoding.UTF8.GetBytes("TRIG");
        await stream.WriteAsync(data, 0, 4);
        await FlashImage();
    }


    // TEMPORARY FOR TESTING
    [SerializeField] Image debugImage;
    public async Task FlashImage()
    {
        if (debugImage == null) return;
        debugImage.color = Color.green;
        await Task.Delay(1000);
        if (debugImage == null) return;
        debugImage.color = Color.white;
    }
}
