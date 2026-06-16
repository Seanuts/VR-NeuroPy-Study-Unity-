using UnityEngine;

public class BrainController : MonoBehaviour
{
    // Selected brain groups
    [SerializeField] GameObject brain;
    [SerializeField] GameObject hippocampus;
    [SerializeField] GameObject temporal_lobe;


    // Materials
    [SerializeField] Material highlight;
    [SerializeField] Material transparent;

    void Start()
    {
        SetMaterialRecursive(brain, transparent);
    }


    public static void SetMaterialRecursive(GameObject obj, Material material)
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

    //void Update()
    //{
    //}

}
