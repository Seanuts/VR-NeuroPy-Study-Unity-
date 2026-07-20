using UnityEngine;
using UnityEditor;
using System.IO;

public class SimpleMaterialAssigner : EditorWindow
{
    [MenuItem("Tools/Material Assigner")]
    public static void ShowWindow()
    {
        GetWindow<SimpleMaterialAssigner>("Material Assigner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Assign Textures to Selected Materials", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Auto-Assign Textures", GUILayout.Height(40)))
        {
            AssignTexturesToSelectedMaterials();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Select your material asset files in the Project window, then click this button. Make sure your textures folder contains files matching the material name (e.g., 'Bed_BaseColor').", MessageType.Info);
    }

    private static void AssignTexturesToSelectedMaterials()
    {
        Object[] selectedObjects = Selection.GetFiltered<Material>(SelectionMode.Assets);

        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Materials Selected", "Please select one or more Material assets in your Project window first.", "OK");
            return;
        }

        int count = 0;

        foreach (Object obj in selectedObjects)
        {
            Material mat = obj as Material;
            if (mat == null) continue;

            // Get material name (e.g., "Bed")
            string matName = mat.name;

            // Search the entire project for textures matching this material name prefix
            string[] textureGuids = AssetDatabase.FindAssets(matName + " t:Texture2D");

            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

                // Assign maps based on your file suffixes
                if (fileName.Contains("basecolor") || fileName.Contains("base_color"))
                {
                    // Standard / URP uses _MainTex or _BaseMap
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
                else if (fileName.Contains("normal"))
                {
                    // Fix texture type to Normal Map if it isn't already
                    FixNormalMapTextureType(path);

                    if (mat.HasProperty("_BumpMap"))
                    {
                        mat.SetTexture("_BumpMap", tex);
                        mat.EnableKeyword("_NORMALMAP"); // Turn on normal mapping in shader
                    }
                }
                else if (fileName.Contains("metallic"))
                {
                    if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", tex);
                }
                else if (fileName.Contains("roughness"))
                {
                    // Note: Unity Standard/URP uses Smoothness (Inverted Roughness map). 
                    // To use true roughness, switch your material shader to a "Roughness Setup" variant.
                    if (mat.HasProperty("_SpecGlossMap")) mat.SetTexture("_SpecGlossMap", tex);
                }
            }
            count++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Success!", $"Processed {count} materials successfully.", "OK");
    }

    private static void FixNormalMapTextureType(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
