using UnityEngine;
using System;

// NOTE: this class is currently not being used
[CreateAssetMenu(fileName = "BrainPreset", menuName = "Scriptable Objects/BrainPreset")]
public class BrainPreset : ScriptableObject
{
    public bool custom = false;
    public Material[] regionMaterials;

    // Ensure correct length
    private void OnEnable()
    {
        if(regionMaterials == null || regionMaterials.Length != Enum.GetValues(typeof(BrainRegion)).Length)
        {
            Array.Resize(ref regionMaterials, Enum.GetValues(typeof(BrainRegion)).Length);
        }
    }
}
