using UnityEngine;
using UnityEditor;

public class ReplaceBrainMaterials
{

    [MenuItem("Tools/Brain/Replace With Placeholder Material")]
    private static void ReplaceMaterials()
    {
        Material placeholder =
            AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Neuro_Materials/GhostTransparent.mat");

        if (placeholder == null)
        {
            Debug.LogError("Placeholder material not found.");
            return;
        }

        Renderer[] renderers =
            Selection.activeGameObject.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] mats = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < mats.Length; i++)
                mats[i] = placeholder;

            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);
        }
    }
}