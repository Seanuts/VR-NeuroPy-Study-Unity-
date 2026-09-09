using UnityEngine;

public class Tracker : MonoBehaviour
{
    [SerializeField] bool debugMode = false;

    public GameObject leftBeam;
    public GameObject rightBeam;

    const float SAMPLE_INTERVAL = 1f / 30f; // Quest Pro eye/face tracking is hardware-capped at ~30Hz
    float sampleTimer = 0f;

    OVRPlugin.EyeGazesState eyeGazesState;
    OVRPlugin.FaceState faceState;
    float[] faceBuf = new float[(int)OVRPlugin.FaceExpression2.Max];

    void Awake()
    {
        leftBeam.SetActive(debugMode);
        rightBeam.SetActive(debugMode);
    }

    void Update()
    {
        if (debugMode) return;

        sampleTimer += Time.deltaTime;
        if (sampleTimer < SAMPLE_INTERVAL) return;
        sampleTimer -= SAMPLE_INTERVAL; // subtract, not reset, to avoid drift

        // Eyes (Layer 2 — raw runtime pose via OVRPlugin)
        if (OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
        {
            var left = eyeGazesState.EyeGazes[0].Pose.ToOVRPose();
            var right = eyeGazesState.EyeGazes[1].Pose.ToOVRPose();
            DataManager.Instance.LogEyeDataLeft(left.position, left.orientation);
            DataManager.Instance.LogEyeDataRight(right.position, right.orientation);
        }
        else
        {
            Debug.LogError("Error during eye tracking");
        }

        // Face (Layer 2 — raw runtime weights via OVRPlugin)
        if (OVRPlugin.GetFaceState2(OVRPlugin.Step.Render, -1, ref faceState) && faceState.Status.IsValid)
        {
            faceState.ExpressionWeights.CopyTo(faceBuf, 0);
            DataManager.Instance.LogFaceData(faceBuf);
        }
        else
        {
            Debug.LogError("Error during face tracking");
        }
    }
}
