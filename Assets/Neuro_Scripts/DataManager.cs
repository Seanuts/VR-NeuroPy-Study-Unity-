using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    void Awake()
    {
        if( DataManager.Instance != null && DataManager.Instance != this )
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    //void Start()
    //{
    //}

    //void Update()
    //{
    //}


}
