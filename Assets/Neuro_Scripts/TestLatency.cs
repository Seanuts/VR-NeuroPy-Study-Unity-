using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


public class TestLatency : MonoBehaviour
{

    [SerializeField] Image img;

    void Start()
    {
        img.color = Color.white; 
    }

    //void Update()
    //{
    //}


    public async void flashGreen()
    {
        if (img == null) return;
        
        img.color = Color.green;
        await Task.Delay(100);
        if(img != null)
        {
            img.color = Color.white;
        }
    }   
}
