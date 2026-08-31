using System;
using System.Collections.Generic;
using UnityEngine;

public enum BrainRegion
{
    FrontalLobe,
    PrimaryMotorCortex,
    Hippocampus,
    Thalamus,
    Amygdala,
    ParietalLobe,
    TemporalLobe,
    OccipitalLobe,
    PrimarySomatosensoryCortex,
    Cerebellum,
    LeftInferiorFrontalGyrus,
    Brainstem,
    RightInferiorFrontalGyrus,
    LeftSuperiorTemporalGyrus,
    RightOccipitalLobe
}

// Helper struct for storing BrainRegion->GameObject mappings
[System.Serializable]
public struct BrainRegionMapping
{
    public BrainRegion region;
    public GameObject obj;
}

public class BrainHighlighter : MonoBehaviour
{
    [SerializeField] BrainOrbit orbiter;
    [SerializeField] GameObject brainModel;
    [SerializeField] List<BrainRegionMapping> brainRegionMapping;
    private Dictionary<BrainRegion, GameObject> brainRegions;

    [SerializeField] Material brainMaterial;
    [SerializeField] Material transparentMaterial;
    [SerializeField] Material highlightMaterial;
    private Material runtimeTransparentMaterial;

    // Create lookup table for fast BrainRegion->GameObject mapping
    public void CreateBrainMapping(){ 
        brainRegions = new Dictionary<BrainRegion, GameObject>();
        foreach (var mapping in brainRegionMapping)
        {
            // Check not duplicate (JIC)
            if (!brainRegions.ContainsKey(mapping.region))
            {
                brainRegions.Add(mapping.region, mapping.obj);
            }
            else
            {
                Debug.Log("Warning: duplicate key found during Brain Region Mapping!");
            }
        }
    }

    void Awake()
    {
        // Convert BRM to dict for fast lookups
        CreateBrainMapping();
        runtimeTransparentMaterial = CreateRuntimeTransparentMaterial();
    }

    // The transparent shader is intentionally used for slides that highlight
    // structures inside the brain. Its asset defaults leave the surface black,
    // specular-preserving, and depth-writing, which makes it inherit the blue
    // Virtual3D environment. Use a runtime copy so the shared project material
    // is not mutated while playtesting.
    private Material CreateRuntimeTransparentMaterial()
    {
        if (transparentMaterial == null)
            return null;

        Material material = new Material(transparentMaterial)
        {
            name = transparentMaterial.name + " (Runtime Neutral)"
        };

        Color shellColor = new Color(0.75f, 0.55f, 0.55f, 1f);
        if (brainMaterial != null)
        {
            if (brainMaterial.HasProperty("_BaseColor"))
                shellColor = brainMaterial.GetColor("_BaseColor");
            else if (brainMaterial.HasProperty("_Color"))
                shellColor = brainMaterial.GetColor("_Color");
        }
        shellColor.a = 1f;

        SetColorIfPresent(material, "_OutlineColor", shellColor);
        SetColorIfPresent(material, "_BaseColor", shellColor);
        SetColorIfPresent(material, "_Color", shellColor);
        SetColorIfPresent(material, "_SpecColor", Color.black);

        // The shader graph's intended Fresnel default is 3, but the serialized
        // material overrides it with 0. Restore the intended transparent shell
        // and prevent sky/environment reflections from tinting it blue.
        SetFloatIfPresent(material, "_FresnelPower", 3f);
        SetFloatIfPresent(material, "_EnvironmentReflections", 0f);
        SetFloatIfPresent(material, "_SpecularHighlights", 0f);
        SetFloatIfPresent(material, "_BlendModePreserveSpecular", 0f);
        SetFloatIfPresent(
            material,
            "_SrcBlend",
            (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(
            material,
            "_DstBlend",
            (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ReceiveShadows", 0f);
        SetFloatIfPresent(material, "_CastShadows", 0f);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_ZWriteControl", 0f);
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetShaderPassEnabled("ShadowCaster", false);

        return material;
    }

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
            material.SetColor(property, value);
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    // Configures interactive brain for the current slide
    public void LoadSlide(Slide slide)
    {
        switch( slide.slideType )
        {
            case SlideType.Question:
                this.gameObject.SetActive(false);
                break;

            case SlideType.Infographic:
                // Reset orientation, make brain visible, highlight target region
                orbiter.ResetRotation();
                this.gameObject.SetActive(true);
                if (slide.makeTransparent)
                {
                    SetMaterialRecursive(
                        brainModel,
                        runtimeTransparentMaterial != null
                            ? runtimeTransparentMaterial
                            : transparentMaterial);
                }
                else
                {
                    SetMaterialRecursive(brainModel, brainMaterial);
                }
                SetBrainRegionMaterial(slide.curRegion, highlightMaterial);
                break;

            case SlideType.Instruction:
                this.gameObject.SetActive(false);
                break;
        }
        
        // NOTE: Not using custom presets
        // If there is a custom preset
        //foreach (BrainRegion reg in Enum.GetValues(typeof(BrainRegion)))
        //{
        //    Material curMat = preset.regionMaterials[(int)reg];
        //    if (curMat == null) curMat = brainMaterial;
        //    SetBrainRegionMaterial(reg, curMat);
        //}
    }

    public void SavePreset(BrainPreset preset)
    {
        foreach (BrainRegion reg in Enum.GetValues(typeof(BrainRegion)))
        {
            if (brainRegions.TryGetValue(reg, out GameObject obj))
            {
                Material mat = ExtractMaterial(obj);
                if (mat == null) mat = brainMaterial;
                preset.regionMaterials[(int)reg] = mat;
            }
            else
            {
                Debug.Log("No GameObject found for BrainRegion " + reg.ToString());
            }
        }
    }

    // Apply material to specfic BrainRegion
    public void SetBrainRegionMaterial(BrainRegion reg,  Material material)
    {
        if (brainRegions.TryGetValue(reg, out GameObject obj))
        {
            SetMaterialRecursive(obj, material);
        }
        else
        {
            Debug.Log("Error applying material to BrainRegion " + reg.ToString());
        }
    }

    // Recursively set material on GameObject 
    public void SetMaterialRecursive(GameObject obj, Material material)
    {
        if (obj == null) return;
        if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
        {
            int matCount = meshRenderer.sharedMaterials.Length;
            if (matCount > 0)
            {
                // Create the array and fill all slots with the asset reference
                Material[] newMaterials = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    newMaterials[i] = material;
                }
                meshRenderer.sharedMaterials = newMaterials;
                // If we are modifying an object permanently in the Editor, mark it dirty
                if (!Application.isPlaying)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(meshRenderer);
#endif
                }
            }
        }
        // Recursively process children
        foreach (Transform child in obj.transform)
        {
            SetMaterialRecursive(child.gameObject, material);
        }
    }

    // Recursively extract first material reference from GameObject
    public Material ExtractMaterial(GameObject obj)
    {
        if (obj == null) return null;
        // Check if the current GameObject has a MeshRenderer
        if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
        {
            // Check sharedMaterials directly to avoid instantiating material copies in memory
            if (meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
            {
                // Return the very first material found in the array
                if (meshRenderer.sharedMaterials[0] != null)
                {
                    return meshRenderer.sharedMaterials[0];
                }
            }
        }
        // Recursively check children
        foreach (Transform child in obj.transform)
        {
            Material foundMaterial = ExtractMaterial(child.gameObject);
            // If a child found a material, pass it up the chain immediately
            if (foundMaterial != null)
            {
                return foundMaterial;
            }
        }
        // Return null if no material was found anywhere in the hierarchy
        return null;
    }

    private void OnDestroy()
    {
        if (runtimeTransparentMaterial != null)
            Destroy(runtimeTransparentMaterial);
    }

}
