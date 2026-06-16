using UnityEngine;

public class EditBrain : MonoBehaviour
{

    [SerializeField] Material transparent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ContextMenu("Apply Default Material")]
    private void ApplyDefaultMaterial()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] mats =
                new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = transparent;
            }

            renderer.sharedMaterials = mats;
        }
    }
}
