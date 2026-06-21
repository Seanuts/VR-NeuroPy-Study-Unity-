using UnityEngine;

#if UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

public class SerialCommunicator : MonoBehaviour
{

#if UNITY_STANDALONE_WIN
    SerialPort serial;
#endif

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
#if UNITY_STANDALONE_WIN
        serial = new SerialPort("COM4", 115200);
        serial.Open();
#endif
    }
    

    public void SendTrigger()
    {
#if UNITY_STANDALONE_WIN
        serial.WriteLine("TRIG");
#else
        Debug.Log("Serial communication not supported in this platform!");
#endif

    }

    // Update is called once per frame
    //void Update()
    //{
    //}
}
