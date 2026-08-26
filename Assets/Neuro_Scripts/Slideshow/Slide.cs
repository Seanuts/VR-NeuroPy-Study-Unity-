using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
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
    public float timeLimit; // In seconds
    public SlideType slideType;

    // Question slide specific
    public string question;
    public Sprite questionGraphic;
    public int correctAnsIndex; // Red, yellow, green, blue (respectively)

    // Information slide specifc
    public Sprite infographic;
    public BrainRegion curRegion;
    public bool makeTransparent = false;

    // Instruction slide
    public Sprite instructionGraphic;
    public bool skippable = true;
}
