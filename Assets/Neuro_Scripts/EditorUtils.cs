using UnityEngine;
using UnityEditor;

public class ReplaceBrainMaterials: EditorWindow
{

    // Helper editor function to replace materials of multi mesh object
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


    // Helper editor functiosn to highlight certain brain regions
    private BrainController brainController;
    private GameObject brain;
    private Material transparent;
    private Material color;
    private BrainRegion target;


    [MenuItem("Tools/Brain/Brain Highlighter")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceBrainMaterials>("Brain Material Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Brain Controller Reference", EditorStyles.boldLabel);
        brainController = (BrainController)EditorGUILayout.ObjectField("Brain Controller", brainController, typeof(BrainController), true);
        
        GUILayout.Label("Brain Reference", EditorStyles.boldLabel);
        brain = (GameObject)EditorGUILayout.ObjectField("Brain", brain, typeof(GameObject), true);

        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // --- SECTION 1: Make Whole Brain Transparent ---
        GUILayout.Label("Action: Reset Entire Brain", EditorStyles.boldLabel);
        transparent = (Material)EditorGUILayout.ObjectField("Transparent Mat", transparent, typeof(Material), false);
        if (GUILayout.Button("Set Entire Brain Transparent", GUILayout.Height(25)))
        {
            SetBrainTransparent();
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // --- SECTION 2: Highlight Specific Region ---
        GUILayout.Label("Action: Highlight Brain Region", EditorStyles.boldLabel);
        // EditorGUILayout.EnumPopup dynamically populates your custom BrainRegion enum choices
        target = (BrainRegion)EditorGUILayout.EnumPopup("Target Region", target);
        color = (Material)EditorGUILayout.ObjectField("Highlight Mat", color, typeof(Material), false);

        if (GUILayout.Button("Apply Material to Selected Region", GUILayout.Height(25)))
        {
            SetBrainRegionColor();
        }
    }

    private void SetBrainTransparent()
    {
        brainController.SetMaterialRecursive(brain, transparent);

    }


    private void SetBrainRegionColor()
    {
        brainController.createBrainMapping();
        brainController.SetBrainRegionMaterial(target, color);
    }


}