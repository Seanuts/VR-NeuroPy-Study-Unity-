using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;

public enum GameModes
{
    INSTRUCTIONS,
    VR,
    DESKTOP,
    HYBRID
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] BrainHighlighter brain;
    
    [SerializeField] SlideshowController vrSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;

    // Game sequence
    [SerializeField] SlideBlock instructionBlock;
    [SerializeField] List<SlideBlock> learningBlocks;
    List<SlideBlock> blockOrder;
    List<GameModes> modeOrder;
    int curModeIndex;

    void Awake()
    {
        // Load as singleton
        if (GameManager.Instance != null && GameManager.Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        GameManager.Instance = this;

        // Randomize learning blocks
        learningBlocks = learningBlocks.OrderBy(x => UnityEngine.Random.value).ToList();
        // Create block sequence
        blockOrder = new List<SlideBlock>{ instructionBlock };
        blockOrder.AddRange(learningBlocks);

        // Randomize block order for VR & Hybrid
        List<GameModes> randomModes = new List<GameModes> { GameModes.HYBRID, GameModes.VR };
        randomModes = randomModes.OrderBy(x => UnityEngine.Random.value).ToList();
        // Create mode sequence
        modeOrder = new List<GameModes> { GameModes.INSTRUCTIONS };
        modeOrder.AddRange(randomModes);
        modeOrder.Add(GameModes.DESKTOP);
    }

    void Start()
    {
        // Start the first mode
        curModeIndex = 0;
        InitializeMode(curModeIndex);
    }

    public void InitializeMode(int state)
    {
        GameModes curMode = modeOrder[state];
        SlideBlock curBlock = blockOrder[state];
        DataManager.Instance.LogEvent(curMode.ToString());

        switch (curMode)
        {
            case GameModes.INSTRUCTIONS:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                brain.gameObject.SetActive(false);
                vrSlideshow.StartSlideshow(state, curBlock);
                break;
            case GameModes.DESKTOP:
                desktopSlideshow.gameObject.SetActive(true);
                vrSlideshow.gameObject.SetActive(false);
                brain.gameObject.SetActive(false);
                desktopSlideshow.StartSlideshow(state, curBlock);
                break;
            case GameModes.HYBRID:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                brain.gameObject.SetActive(false);
                vrSlideshow.StartSlideshow(state, curBlock);
                break;
            case GameModes.VR:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                brain.gameObject.SetActive(true);
                vrSlideshow.StartSlideshow(state, curBlock);
                break;
        }
    }
   
    // Triggered by SlideshowController to alert GameManager of updates
    public void SlideChanged(Slide newSlide)
    {
        DataManager.Instance.LogEvent(newSlide.name);
        if (modeOrder[curModeIndex] == GameModes.VR)
        {
            brain.LoadSlide(newSlide);
        }
    }

    // Triggered by SlideshowController to alert GameManager of mode finishing
    public void ModeFinished()
    {
        // Advance to the next mode
        curModeIndex++;
        if(curModeIndex >= modeOrder.Count)
        {
            GameFinished();
        } 
        else
        {
            InitializeMode(curModeIndex);
        }
    }

    async void GameFinished()
    {
        // Record final event
        DataManager.Instance.LogEvent("FINISHED");

        // Load lobby scene again
        await SceneManager.LoadSceneAsync("ThankYou");
    }

    public GameModes CurrentMode()
    {
        return modeOrder[curModeIndex];
    }
}
