using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class SlideshowController : MonoBehaviour
{
    [SerializeField] SlideTimer slideTimer;

    // Default break slide
    [SerializeField] Slide breakSlide;

    // For question
    [SerializeField] GameObject questionLayout;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] Image questionGraphic;
    
    // For infographic
    [SerializeField] GameObject infographicLayout;
    [SerializeField] Image infographic;
    [SerializeField] Image graphicBlocker;
    
    // For instruction
    [SerializeField] GameObject instructionLayout;
    [SerializeField] Image instructGraphic;

    // Slideshow management
    SlideBlock curBlock;
    List<Slide> slideOrder;
    int curSlideIndex;

    public void StartSlideshow(int curMode, SlideBlock block)
    {
        curBlock = block;
        
        // Add break for middle slides
        if(curMode >= 1 && curMode <= 3)
        {
            // Only add break for final 2 slide transitions
            slideOrder = new List<Slide> { breakSlide };
        }
        else
        {
            slideOrder = new List<Slide> {};
        }
        // Add infographic slides
        slideOrder.AddRange(
            curBlock.infographicSlides
        );
        // Add randomized question slides
        List<Slide> randomizedQuestions = curBlock.questionSlides.OrderBy(x => UnityEngine.Random.value).ToList();
        slideOrder.AddRange(
            randomizedQuestions
        );
        
        // Start at slide 0
        curSlideIndex = 0;
        DisplayCurrentSlide();
    }

    void DisplayCurrentSlide()
    {
        Slide slide = slideOrder[curSlideIndex];

        // Let GameManager know slide is changing
        GameManager.Instance.SlideChanged(slide);

        // Display content
        switch (slide.slideType)
        {
            case SlideType.Instruction:
                instructionLayout.SetActive(true);
                questionLayout.SetActive(false);
                infographicLayout.SetActive(false);
                instructGraphic.sprite = slide.instructionGraphic;
                break;

            case SlideType.Question:
                questionLayout.SetActive(true);
                infographicLayout.SetActive(false);
                instructionLayout.SetActive(false);
                questionText.text = slide.question;
                questionGraphic.sprite = slide.questionGraphic;
                break;

            // TODO: Update so that only blocks graphics durign VR mode
            case SlideType.Infographic:
                infographicLayout.SetActive(true);
                questionLayout.SetActive(false);
                instructionLayout.SetActive(false);
                infographic.sprite = slide.infographic;
                // Block brain picture if VR mode
                //if (GameManager.Instance.CurrentMode() == GameModes.VR)
                //{
                //    graphicBlocker.gameObject.SetActive(true);
                //}
                //else
                //{
                //    graphicBlocker.gameObject.SetActive(false);
                //}
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
            GameManager.Instance.ModeFinished();
        }
        else
        {
            DisplayCurrentSlide();
        }
    }

    public void QuestionAnswered(int index)
    {
        // 0 - Red; 1 - Yellow; 2 - Green; 3 - Blue
        Slide curSlide = slideOrder[curSlideIndex];
        if (index == curSlide.correctAnsIndex)
        {
            DataManager.Instance.LogResultData(curSlide.name, "correct");
        }
        else
        {
            DataManager.Instance.LogResultData(curSlide.name, "incorrect");
        }
        NextSlide();
    }

    public void TimerExpired()
    {
        Slide curSlide = slideOrder[curSlideIndex];
        if (curSlide.slideType == SlideType.Question)
        {
            // If timer expired on question slide then timeout (incorrect)
            DataManager.Instance.LogResultData(curSlide.name, "timeout");
        }
        NextSlide();
    }
}
