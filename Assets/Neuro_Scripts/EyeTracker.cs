using UnityEngine;

public class EyeTracker : MonoBehaviour
{
    public GameObject leftEye;
    public GameObject rightEye;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
    //}

    // Update is called once per frame
    void Update()
    {
        DataManager.Instance.LogEyeDataLeft(leftEye.transform.localPosition, leftEye.transform.localRotation);
        DataManager.Instance.LogEyeDataRight(rightEye.transform.localPosition, rightEye.transform.localRotation);
    }
}
