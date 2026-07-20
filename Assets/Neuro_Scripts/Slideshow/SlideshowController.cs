using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;


[Serializable]
public class SlideData
{
    public string slideName;
    public string result;
}

[Serializable]
public class BlockData
{
    public SlideData[] slides = new SlideData[3];
    public int totalCorrect = 0;
    public int totalIncorrect = 0;
    public int totalTimeout = 0;
}

public class SlideshowController : MonoBehaviour
{
    [SerializeField] SlideTimer slideTimer;
    
    // For question
    [SerializeField] GameObject questionLayout;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] Image questionGraphic;
    
    // For infographic
    [SerializeField] GameObject infographicLayout;
    [SerializeField] Image infographic;
    
    // For instruction
    [SerializeField] GameObject instructionLayout;
    [SerializeField] TextMeshProUGUI instructionText;

    // Slideshow management
    SlideBlock curBlock;
    List<Slide> slideOrder;
    int curSlideIndex;

    // Results
    BlockData results;

    public void StartSlideshow(Slide startSlide, SlideBlock block)
    {
        // Merge slides
        curBlock = block;
        slideOrder = new List<Slide> { startSlide };
        slideOrder.AddRange(
            curBlock.infographicSlides
        );
        slideOrder.AddRange(
            curBlock.questionSlides
        );
        // Start at slide 0
        curSlideIndex = 0;
        // Initialize result data
        results = new BlockData();
        DisplayCurrentSlide();
    }

    void DisplayCurrentSlide()
    {
        Slide slide = slideOrder[curSlideIndex];
        
        // Display content
        switch (slide.slideType)
        {
            case SlideType.Instruction:
                instructionLayout.SetActive(true);
                questionLayout.SetActive(false);
                infographicLayout.SetActive(false);
                instructionText.text = slide.instructions;
                break;

            case SlideType.Question:
                questionLayout.SetActive(true);
                infographicLayout.SetActive(false);
                instructionLayout.SetActive(false);
                questionText.text = slide.question;
                questionGraphic.sprite = slide.questionGraphic;
                break;

            case SlideType.Infographic:
                infographicLayout.SetActive(true);
                questionLayout.SetActive(false);
                instructionLayout.SetActive(false);
                infographic.sprite = slide.infographic;
                break;
        }

        // Start the timer
        slideTimer.StartTimer(slide.timeLimit);
    }

    // Called by buttons/timer to advance slideshow
    public void NextSlide()
    {
        curSlideIndex++;
        // Check if slideshow done
        if(curSlideIndex >= slideOrder.Count)
        {
            // Alert GameManager of slideshow completion
            GameManager.Instance.ModeFinished(results);
        }
        else
        {
            // Alert GameManager of the newly changed slide
            GameManager.Instance.SlideChanged(slideOrder[curSlideIndex]);
            DisplayCurrentSlide();
        }
    }

    public void QuestionAnswered(int index)
    {
        // 0 - Red; 1 - Yellow; 2 - Green; 3 - Blue
        Slide curSlide = slideOrder[curSlideIndex];
        if (index == curSlide.correctAnsIndex)
        {
            results.slides[curSlideIndex - 4] = new SlideData();
            results.slides[curSlideIndex - 4].slideName = curSlide.name;
            results.slides[curSlideIndex - 4].result = "correct";
            results.totalCorrect++;
        }
        else
        {
            results.slides[curSlideIndex - 4] = new SlideData();
            results.slides[curSlideIndex - 4].slideName = curSlide.name;
            results.slides[curSlideIndex - 4].result = "incorrect";
            results.totalIncorrect++;
        }
        NextSlide();
    }

    public void TimerExpired()
    {
        if (slideOrder[curSlideIndex].slideType == SlideType.Question)
        {
            // If timer expired on question slide then incorrect
            results.slides[curSlideIndex - 4] = new SlideData();
            results.slides[curSlideIndex - 4].slideName = slideOrder[curSlideIndex].name;
            results.slides[curSlideIndex - 4].result = "timeout";
            results.totalCorrect++;
        }
        NextSlide();
    }
}
