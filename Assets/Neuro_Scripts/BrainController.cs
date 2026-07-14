using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BrainRegionMapping
{
    public BrainRegion region;
    public GameObject obj;
}

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

public class BrainController : MonoBehaviour
{
    [SerializeField] GameObject brainModel;
    [SerializeField] List<BrainRegionMapping> brainRegionMapping;
    Dictionary<BrainRegion, GameObject> brainRegions;

    [SerializeField] List<Material> highlightMaterials;
    [SerializeField] Material transparent;


    public void createBrainMapping(){ 
        brainRegions = new Dictionary<BrainRegion, GameObject>();
        foreach (var mapping in brainRegionMapping)
        {
            // check not duplicate (JIC)
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
        // convert BRM to dict for fast lookups
        createBrainMapping();
        // ensure transparent is default material
        SetMaterialRecursive(brainModel, transparent);
    }

    public void UpdateHighlighting(Slide slide)
    {
        if (!this.gameObject.activeInHierarchy)
        {
            return;
        }

        // set base as transparent
        SetMaterialRecursive(brainModel, transparent);
        switch (slide.slideType)
        {
            case SlideType.Infographic:
                SetBrainRegionMaterial(slide.curRegion, highlightMaterials[0]);
                break;

            case SlideType.Question:
                // TODO: see how this affects overalapping reginos
                for(int i = 0; i < 4; i++)
                {
                    SetBrainRegionMaterial(slide.answerChoiceRegions[i], highlightMaterials[i]);
                }
                break;
            case SlideType.Instruction:
                break;
        }

    }

    public void SetBrainRegionMaterial(BrainRegion reg,  Material material)
    {
        if (brainRegions.TryGetValue(reg, out GameObject obj))
        {
            SetMaterialRecursive(obj, material);
        }
        else
        {
            Debug.Log("Error highlighting brain object region!");
        }
    }

    public void SetMaterialRecursive(GameObject obj, Material material)
    {
        if (obj == null)
            return;
        // Check for MeshRenderer
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material = material;
        }
        // Recursively process children
        foreach (Transform child in obj.transform)
        {
            SetMaterialRecursive(child.gameObject, material);
        }
    }

}
