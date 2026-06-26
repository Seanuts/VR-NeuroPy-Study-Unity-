using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public enum SlideType
{
    Question,
    Infographic,
    Instruction
}

// Ideally should have been split into subclasses for each slidetype
[CreateAssetMenu(fileName = "Slide", menuName = "Scriptable Objects/Slide")]
public class Slide : ScriptableObject
{
    public float timeLimit; // in seconds
    public SlideType slideType;

    // question slide specific
    public string question;
    public Sprite questionGraphic;
    public List<BrainRegion> answerChoiceRegions;
    public int correctAnsIndex; // red, yellow, green, blue (respectively)

    // information slide specifc
    public Sprite infographic;
    public BrainRegion curRegion;

    // instruction slide
    [TextArea(5,20)] public string instructions;
}
