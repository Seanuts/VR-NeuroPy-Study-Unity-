using UnityEngine;
using Meta.XR.Util;

public class Tracker : MonoBehaviour
{
    [SerializeField] bool debugMode = false;

    public GameObject leftEye;
    public GameObject rightEye;

    public GameObject leftBeam;
    public GameObject rightBeam;

    [SerializeField] private OVRFaceExpressions face;
    private float[] faceBuf = new float[(int)OVRFaceExpressions.FaceExpression.Max];

    void Awake()
    {
        if (debugMode)
        {
            leftBeam.SetActive(true);
            rightBeam.SetActive(true);
        }
        else
        {
            leftBeam.SetActive(false);
            rightBeam.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (debugMode) return;

        // Eyes
        DataManager.Instance.LogEyeDataLeft(leftEye.transform.localPosition, leftEye.transform.localRotation);
        DataManager.Instance.LogEyeDataRight(rightEye.transform.localPosition, rightEye.transform.localRotation);

        // Face
        if (face.FaceTrackingEnabled && face.ValidExpressions)
        {
            face.CopyTo(faceBuf, 0);
            DataManager.Instance.LogFaceData(faceBuf);
        }
        else
        {
            Debug.LogError("Error during face tracking");
        }
    }
}  