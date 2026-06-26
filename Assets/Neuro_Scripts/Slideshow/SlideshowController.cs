using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;


public class SlideshowController : MonoBehaviour
{
    [SerializeField] SlideTimer slideTimer;
    
    // for question
    [SerializeField] GameObject questionLayout;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] Image questionGraphic;
    
    // for infographic
    [SerializeField] GameObject infographicLayout;
    [SerializeField] Image infographic;
    
    // for instruction
    [SerializeField] GameObject instructionLayout;
    [SerializeField] TextMeshProUGUI instructionText;

    // slideshow management
    SlideBlock curBlock;
    List<Slide> slideOrder;
    int curSlideIndex;

    // data collection
    int correctAnswers;
    int wrongAnswers;

    //void Start()
    //{
    //}

    //void Update()
    //{
    //}

    public void StartSlideshow(Slide startSlide, SlideBlock block)
    {
        // get block and randomize order of slides
        curBlock = block;
        slideOrder = new List<Slide> { startSlide };
        slideOrder.AddRange(
            curBlock.infographicSlides
                .OrderBy(x => UnityEngine.Random.value)
        );
        slideOrder.AddRange(
            curBlock.questionSlides
                .OrderBy(x => UnityEngine.Random.value)
        );

        // initialize internal vars
        curSlideIndex = 0;
        correctAnswers = 0;
        wrongAnswers = 0;
        DisplayCurrentSlide();
    }

    void DisplayCurrentSlide()
    {
        Slide slide = slideOrder[curSlideIndex];
        
        // display content
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
        // update the highlighting based on current slide information
        GameManager.Instance.InteractiveBrain.UpdateHighlighting(slide);

        // start the timer
        slideTimer.StartTimer(slide.timeLimit);
    }

    // called by buttons/timer to advance slideshow
    public void NextSlide()
    {
        curSlideIndex++;
        // check if slideshow done
        if(curSlideIndex >= slideOrder.Count)
        {
            ModeData data = new ModeData();
            data.questionsCorrect = correctAnswers;
            data.questionsWrong = wrongAnswers;
            GameManager.Instance.ModeFinished(data);
        }
        else
        {
            DisplayCurrentSlide();
        }
    }

    public void QuestionAnswered(int index)
    {
        // 0 - red; 1 - yellow; 2 - green; 3 - blue
        Slide curSlide = slideOrder[curSlideIndex];
        if (index == curSlide.correctAnsIndex)
        {
            correctAnswers++;
        }
        else
        {
            wrongAnswers++;
        }
        NextSlide();
    }

    public void TimerExpired()
    {
        if (slideOrder[curSlideIndex].slideType == SlideType.Question)
        {
            // if timer expired on question slide then incorrect
        }
        NextSlide();
    }
}
