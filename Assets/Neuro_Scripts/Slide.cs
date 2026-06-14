using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


// Ideally should have been split into subclasses for each slidetype
public enum SlideType
{
    Question,
    Infographic,
    Instruction
}


[CreateAssetMenu(fileName = "Slide", menuName = "Scriptable Objects/Slide")]
public class Slide : ScriptableObject
{
    [Tooltip("Time limit in seconds")]
    public float timeLimit;
    public SlideType slideType;

    // question slide specific
    public string question;
    public List<string> answerChoices;
    public int correctAnswerIndex;

    // information slide specifc
    public Sprite infographic;

    // instruction slide
    [TextArea(5,20)] public string instruction;

    private void OnValidate()
    {
        // ensure between 1-4 answers if question
        if ( 
            slideType == SlideType.Question && 
            (answerChoices.Count > 4 || answerChoices.Count < 1) 
        )
        {
            Debug.LogError($"Invalid answer choice count for {name}! Must be between 1 and 4");   
        }
    }
}
