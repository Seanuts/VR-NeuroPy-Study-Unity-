using UnityEngine;
using UnityEditor;
using UnityEditor.UI;

public class BrainEditor: EditorWindow
{
    // For all util functions
    private BrainHighlighter brainHighlighter;
    private GameObject brain;

    // Entire model editor
    private Material mat1;

    // Specfic region editor
    private BrainRegion target;
    private Material mat2;

    // To save a specfic region configuration
    private Slide slide;


    [MenuItem("Tools/Brain Editor")]
    public static void ShowWindow()
    {
        GetWindow<BrainEditor>("Brain Material Editor");
    }

    private void OnGUI()
    {
        // Need this for both functions
        GUILayout.Label("Brain Highlighter Reference", EditorStyles.boldLabel);
        brainHighlighter = (BrainHighlighter)EditorGUILayout.ObjectField("Brain Controller", brainHighlighter, typeof(BrainHighlighter), true);
        GUILayout.Label("Brain Model Reference", EditorStyles.boldLabel);
        brain = (GameObject)EditorGUILayout.ObjectField("Brain Model", brain, typeof(GameObject), true);
        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // Function to apply to entire brain
        GUILayout.Label("Action: Apply to all Regions", EditorStyles.boldLabel);
        mat1 = (Material)EditorGUILayout.ObjectField("Material", mat1, typeof(Material), false);
        if (GUILayout.Button("Apply", GUILayout.Height(25)))
        {
            ApplyEntireBrain();
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // Function to apply to specific brain region
        GUILayout.Label("Action: Apply to Specific Region", EditorStyles.boldLabel);
        target = (BrainRegion)EditorGUILayout.EnumPopup("Target Region", target);
        mat2 = (Material)EditorGUILayout.ObjectField("Material", mat2, typeof(Material), false);
        if (GUILayout.Button("Apply", GUILayout.Height(25)))
        {
            ApplyRegion();
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // Function to load current brain as preset
        GUILayout.Label("Action: Save Region Preset", EditorStyles.boldLabel);
        slide = (Slide)EditorGUILayout.ObjectField("Slide", slide, typeof(Slide), false);
        if (GUILayout.Button("Save", GUILayout.Height(25)))
        {
            SaveConfiguration();
        }

    }

    private void ApplyEntireBrain()
    {
        if (brainHighlighter == null || brain == null || mat1 == null)
        {
            Debug.LogWarning("Invalid/missing arguments!");
        }
        brainHighlighter.SetMaterialRecursive(brain, mat1);
    }

    private void ApplyRegion()
    {
        if (brainHighlighter == null || mat2 == null)
        {
            Debug.LogWarning("Invalid/missing arguments!");
        }
        brainHighlighter.CreateBrainMapping();
        brainHighlighter.SetBrainRegionMaterial(target, mat2);
    }

    private void SaveConfiguration()
    {
        if (brainHighlighter == null || slide == null)
        {
            Debug.LogWarning("Invalid/missing arguments!");
        }
        if (slide.preset == null)
        {
            slide.preset = new BrainPreset();
        }
        brainHighlighter.CreateBrainMapping();
        brainHighlighter.SavePreset(slide.preset);
        // Save changes to disk
        EditorUtility.SetDirty(slide);
        AssetDatabase.SaveAssets();
    }
}