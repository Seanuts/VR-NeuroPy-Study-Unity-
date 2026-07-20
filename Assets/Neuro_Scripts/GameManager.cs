using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[Serializable]
public class GameData
{
    public BlockData vr;
    public BlockData hybrid;
    public BlockData desktop;
    public int totalCorrect = 0;
    public int totalIncorrect = 0;
    public int totalTimeout = 0;
}

public enum GameModes
{
    VR,
    Desktop,
    Hybrid
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] BrainHighlighter brain;
    
    [SerializeField] SlideshowController vrSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;
    
    // Instruction slides
    [SerializeField] Slide vrIntroSlide;
    [SerializeField] Slide desktopIntroSlide;
    [SerializeField] Slide hybridIntroSlide;

    // Game sequence
    [SerializeField] List<SlideBlock> blockOrder;
    List<GameModes> modeOrder = new List<GameModes> { GameModes.VR, GameModes.Desktop, GameModes.Hybrid};
    int curModeIndex;

    // Data collection
    GameData gameResults;

    void Awake()
    {
        // Load as singleton
        if( GameManager.Instance != null && GameManager.Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        GameManager.Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Randomize mode & block order
        blockOrder = blockOrder.OrderBy(x => UnityEngine.Random.value).ToList();
        modeOrder = modeOrder.OrderBy(x => UnityEngine.Random.value).ToList();

        // Initialize data collection vars
        gameResults = new GameData();
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
        switch (curMode)
        {
            case GameModes.Desktop:
                desktopSlideshow.gameObject.SetActive(true);
                vrSlideshow.gameObject.SetActive(false);
                brain.gameObject.SetActive(false);
                desktopSlideshow.StartSlideshow(desktopIntroSlide, curBlock);
                break;
            case GameModes.Hybrid:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                brain.gameObject.SetActive(false);
                vrSlideshow.StartSlideshow(hybridIntroSlide, curBlock);
                break;
            case GameModes.VR:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                brain.gameObject.SetActive(true);
                vrSlideshow.StartSlideshow(vrIntroSlide, curBlock);
                break;
        }
        DataManager.Instance.LogEvent(curMode.ToString());
    }
   
    // Triggered by SlideshowController to alert GameManager of updates
    public void SlideChanged(Slide newSlide)
    {
        DataManager.Instance.LogEvent(newSlide.name);
        brain.LoadSlide(newSlide);
    }

    // Triggered by SlideshowController to alert GameManager of mode finishing
    public void ModeFinished(BlockData results)
    {
        // Record data
        gameResults.totalCorrect += results.totalCorrect;
        gameResults.totalIncorrect += results.totalIncorrect;
        gameResults.totalTimeout += results.totalTimeout;

        // Convert nested dict to a string
        GameModes curMode = modeOrder[curModeIndex];
        switch (curMode)
        {
            case GameModes.VR:
                gameResults.vr = results; break;
            case GameModes.Hybrid:
                gameResults.hybrid = results; break;
            case GameModes.Desktop:
                gameResults.desktop = results; break;
        }

        // Advance to the next mode
        curModeIndex++;
        if(curModeIndex >= 3)
        {
            GameFinished();
        } 
        else
        {
            InitializeMode(curModeIndex);
        }
    }

    void GameFinished()
    {
        // Finalize data
        DataManager.Instance.LogEvent("finished");
        DataManager.Instance.LogResultData(gameResults);
    }
}
