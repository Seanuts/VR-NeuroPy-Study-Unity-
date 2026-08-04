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

    // For question creation
    private Material qmat1;
    private Material qmat2;
    private Material qmat3;
    private Material qmat4;
    private BrainRegion qtar1;
    private BrainRegion qtar2;
    private BrainRegion qtar3;
    private BrainRegion qtar4;


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

        // Function to create question configuration
        GUILayout.Label("Action: Question Image Creator Tool", EditorStyles.boldLabel);
        qmat1 = (Material)EditorGUILayout.ObjectField("Material", qmat1, typeof(Material), false);
        qmat2 = (Material)EditorGUILayout.ObjectField("Material", qmat2, typeof(Material), false);
        qmat3 = (Material)EditorGUILayout.ObjectField("Material", qmat3, typeof(Material), false);
        qmat4 = (Material)EditorGUILayout.ObjectField("Material", qmat4, typeof(Material), false);

        qtar1 = (BrainRegion)EditorGUILayout.EnumPopup("Region", qtar1);
        qtar2 = (BrainRegion)EditorGUILayout.EnumPopup("Region", qtar2);
        qtar3 = (BrainRegion)EditorGUILayout.EnumPopup("Region", qtar3);
        qtar4 = (BrainRegion)EditorGUILayout.EnumPopup("Region", qtar4);

        if (GUILayout.Button("Apply", GUILayout.Height(25)))
        {
            QuestionCreateHelper();
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.Space(5);

        // NOTE: Not using custom presets
        // Function to load current brain as preset
        //GUILayout.Label("Action: Save Region Preset", EditorStyles.boldLabel);
        //slide = (Slide)EditorGUILayout.ObjectField("Slide", slide, typeof(Slide), false);
        //if (GUILayout.Button("Save", GUILayout.Height(25)))
        //{
        //    SaveConfiguration();
        //}

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

    // NOTE: It was late at night so I jsut coded this fast, def not polished
    private void QuestionCreateHelper()
    {
        brainHighlighter.CreateBrainMapping();
        brainHighlighter.SetBrainRegionMaterial(qtar1, qmat1);
        brainHighlighter.SetBrainRegionMaterial(qtar2, qmat2);
        brainHighlighter.SetBrainRegionMaterial(qtar3, qmat3);
        brainHighlighter.SetBrainRegionMaterial(qtar4, qmat4);
    }

    // NOTE: Not using custom presets
    //private void SaveConfiguration()
    //{
    //    if (brainHighlighter == null || slide == null)
    //    {
    //        Debug.LogWarning("Invalid/missing arguments!");
    //    }
    //    if (slide.preset == null)
    //    {
    //        slide.preset = new BrainPreset();
    //    }
    //    brainHighlighter.CreateBrainMapping();
    //    brainHighlighter.SavePreset(slide.preset);
    //    // Save changes to disk
    //    EditorUtility.SetDirty(slide);
    //    AssetDatabase.SaveAssets();
    //}
}