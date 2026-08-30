using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class SlideshowController : MonoBehaviour
{
    [SerializeField] SlideTimer slideTimer;

    public Canvas InteractionCanvas => GetComponentInParent<Canvas>();

    public RectTransform InteractionRect
    {
        get
        {
            RectTransform ownRect = GetComponent<RectTransform>();
            if (ownRect != null)
                return ownRect;

            Canvas canvas = InteractionCanvas;
            return canvas != null ? canvas.transform as RectTransform : null;
        }
    }

    // Global slides
    [SerializeField] Slide breakSlide;
    [SerializeField] Slide baselineSlide;

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
    [SerializeField] GameObject nextButtonInstruct;

    // Slideshow management
    SlideBlock curBlock;
    List<Slide> slideOrder;
    int curSlideIndex;
    int lastAdvanceFrame = -1;

    public void StartSlideshow(int curMode, SlideBlock block)
    {
        curBlock = block;


        slideOrder = new List<Slide> {};
        // Only add break for final 2 blocks
        if(curMode >= 2)
        {
            slideOrder.Add(breakSlide);
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

        // Add baseline slide if instruction block
        if( GameManager.Instance.CurrentMode() == GameModes.INSTRUCTIONS)
        {
            slideOrder.Add(baselineSlide);
        }
        
        // Start at slide 0
        curSlideIndex = 0;
        lastAdvanceFrame = -1;
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
                // Toggle next button depending on if instruction is skippable
                if (!slide.skippable)
                {
                    nextButtonInstruct.SetActive(false);
                }
                else
                {
                    nextButtonInstruct.SetActive(true);
                }
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
                // Block brain picture if VR mode
                if (GameManager.Instance.CurrentMode() == GameModes.MIXED_3D || GameManager.Instance.CurrentMode() == GameModes.VIRTUAL_3D){
                    graphicBlocker.gameObject.SetActive(true);
                }
                else{
                    graphicBlocker.gameObject.SetActive(false);
                }
                break;
        }

        // Start the timer
        slideTimer.StartTimer(slide.timeLimit);
    }

    // Called by buttons/timer to advance slideshow
    public void NextSlide()
    {
        if (slideOrder == null || slideOrder.Count == 0 ||
            curSlideIndex < 0 || curSlideIndex >= slideOrder.Count ||
            lastAdvanceFrame == Time.frameCount)
            return;

        lastAdvanceFrame = Time.frameCount;
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
        if (slideOrder == null || curSlideIndex < 0 || curSlideIndex >= slideOrder.Count ||
            lastAdvanceFrame == Time.frameCount)
            return;

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
        if (slideOrder == null || curSlideIndex < 0 || curSlideIndex >= slideOrder.Count ||
            lastAdvanceFrame == Time.frameCount)
            return;

        Slide curSlide = slideOrder[curSlideIndex];
        if (curSlide.slideType == SlideType.Question)
        {
            // If timer expired on question slide then timeout (incorrect)
            DataManager.Instance.LogResultData(curSlide.name, "timeout");
        }
        NextSlide();
    }
}
