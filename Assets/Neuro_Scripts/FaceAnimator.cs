using System.Collections.Generic;
using UnityEngine;

public class FaceAnimator : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer headMesh;
    [SerializeField] private OVRFaceExpressions faceExpressions;

    private float[] faceBuf;
    private readonly Dictionary<int, int> ovrToMeshIndexMap = new Dictionary<int, int>();

    private void Awake()
    {
        if (headMesh == null)
            headMesh = GetComponentInChildren<SkinnedMeshRenderer>();

        int expressionCount = (int)OVRFaceExpressions.FaceExpression.Max;
        faceBuf = new float[expressionCount];

        BuildNameMapping();
    }

    private void BuildNameMapping()
    {
        // Explicitly map OVRFaceExpressions enum entries to your model's specific blendshape names
        var customMappings = new Dictionary<OVRFaceExpressions.FaceExpression, string>()
        {
            // --- Eyes & Brows ---
            { OVRFaceExpressions.FaceExpression.EyesClosedL, "ExpressionBlendshapes.EyeBlink_L" },
            { OVRFaceExpressions.FaceExpression.EyesClosedR, "ExpressionBlendshapes.EyeBlink_R" },
            { OVRFaceExpressions.FaceExpression.LidTightenerL, "ExpressionBlendshapes.EyeSquint_L" },
            { OVRFaceExpressions.FaceExpression.LidTightenerR, "ExpressionBlendshapes.EyeSquint_R" },
            { OVRFaceExpressions.FaceExpression.EyesLookDownL, "ExpressionBlendshapes.EyeDown_L" },
            { OVRFaceExpressions.FaceExpression.EyesLookDownR, "ExpressionBlendshapes.EyeDown_R" },
            { OVRFaceExpressions.FaceExpression.EyesLookUpL, "ExpressionBlendshapes.EyeUp_L" },
            { OVRFaceExpressions.FaceExpression.EyesLookUpR, "ExpressionBlendshapes.EyeUp_R" },
            { OVRFaceExpressions.FaceExpression.EyesLookLeftL, "ExpressionBlendshapes.EyeIn_L" },
            { OVRFaceExpressions.FaceExpression.EyesLookRightR, "ExpressionBlendshapes.EyeIn_R" },
            { OVRFaceExpressions.FaceExpression.EyesLookRightL, "ExpressionBlendshapes.EyeOut_L" },
            { OVRFaceExpressions.FaceExpression.EyesLookLeftR, "ExpressionBlendshapes.EyeOut_R" },

            { OVRFaceExpressions.FaceExpression.BrowLowererL, "ExpressionBlendshapes.BrowsD_L" },
            { OVRFaceExpressions.FaceExpression.BrowLowererR, "ExpressionBlendshapes.BrowsD_R" },
            { OVRFaceExpressions.FaceExpression.OuterBrowRaiserL, "ExpressionBlendshapes.BrowsU_L" },
            { OVRFaceExpressions.FaceExpression.OuterBrowRaiserR, "ExpressionBlendshapes.BrowsU_R" },
            { OVRFaceExpressions.FaceExpression.InnerBrowRaiserL, "ExpressionBlendshapes.BrowsU_C" },

            // --- Cheeks & Nose ---
            { OVRFaceExpressions.FaceExpression.CheekPuffL, "ExpressionBlendshapes.Puff" },
            { OVRFaceExpressions.FaceExpression.CheekPuffR, "ExpressionBlendshapes.Puff" },
            { OVRFaceExpressions.FaceExpression.CheekRaiserL, "ExpressionBlendshapes.CheekSquint_L" },
            { OVRFaceExpressions.FaceExpression.CheekRaiserR, "ExpressionBlendshapes.CheekSquint_R" },
            { OVRFaceExpressions.FaceExpression.NoseWrinklerL, "ExpressionBlendshapes.Sneer_L" },
            { OVRFaceExpressions.FaceExpression.NoseWrinklerR, "ExpressionBlendshapes.Sneer_R" },

            // --- Jaw & Chin ---
            { OVRFaceExpressions.FaceExpression.JawDrop, "ExpressionBlendshapes.JawOpen" },
            { OVRFaceExpressions.FaceExpression.ChinRaiserB, "ExpressionBlendshapes.ChinRaise_L" },
            { OVRFaceExpressions.FaceExpression.ChinRaiserT, "ExpressionBlendshapes.ChinRaise_R" },

            // --- Mouth & Lips ---
            { OVRFaceExpressions.FaceExpression.LipPressorL, "ExpressionBlendshapes.LipsTogether" },
            { OVRFaceExpressions.FaceExpression.LipPressorR, "ExpressionBlendshapes.LipsTogether" },
            { OVRFaceExpressions.FaceExpression.MouthRight, "ExpressionBlendshapes.MouthRight" },
            { OVRFaceExpressions.FaceExpression.MouthLeft, "ExpressionBlendshapes.MouthLeft" }
        };

        // Pre-cache string indices once at initialization
        foreach (var kvp in customMappings)
        {
            int ovrIndex = (int)kvp.Key;
            int meshIndex = headMesh.sharedMesh.GetBlendShapeIndex(kvp.Value);

            if (meshIndex != -1)
            {
                ovrToMeshIndexMap[ovrIndex] = meshIndex;
            }
            else
            {
                Debug.LogWarning($"[ZeroBloatFaceDriver] Blendshape '{kvp.Value}' not found on head mesh.");
            }
        }
    }

    private void LateUpdate()
    {
        if (faceExpressions == null || !faceExpressions.FaceTrackingEnabled || !faceExpressions.ValidExpressions)
            return;

        // Copy raw weights to buffer
        faceExpressions.CopyTo(faceBuf, 0);

        // Apply pre-mapped weights to head mesh
        foreach (var pair in ovrToMeshIndexMap)
        {
            int ovrIndex = pair.Key;
            int meshIndex = pair.Value;

            headMesh.SetBlendShapeWeight(meshIndex, faceBuf[ovrIndex] * 100f);
        }
    }
}