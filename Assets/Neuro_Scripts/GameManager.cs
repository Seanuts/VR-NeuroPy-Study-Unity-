using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public struct ModeData
{
    public GameModes mode;
    public int questionsCorrect;
    public int questionsWrong;
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

    [SerializeField] public BrainController InteractiveBrain;
    
    [SerializeField] SlideshowController vrSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;
    
    // Instruction slides
    [SerializeField] Slide vrIntroSlide;
    [SerializeField] Slide desktopIntroSlide;
    [SerializeField] Slide hybridIntroSlide;

    // Game sequence
    [SerializeField] List<SlideBlock> blockOrder;
    List<GameModes> modeOrder = new List<GameModes> { GameModes.VR, GameModes.Desktop, GameModes.Hybrid };
    int curState;

    // Data collection
    ModeData vrData;
    ModeData desktopData;
    ModeData hybridData;

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
    }

    void Start()
    {
        // Start the first mode
        curState = 0;
        InitializeMode(curState);
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
                InteractiveBrain.gameObject.SetActive(false);
                desktopSlideshow.StartSlideshow(desktopIntroSlide, curBlock);
                break;
            
            case GameModes.Hybrid:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                InteractiveBrain.gameObject.SetActive(false);
                vrSlideshow.StartSlideshow(hybridIntroSlide, curBlock);
                break;
            
            case GameModes.VR:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                InteractiveBrain.gameObject.SetActive(true);
                vrSlideshow.StartSlideshow(vrIntroSlide, curBlock);
                break;
        }
    }
   
    // Activated by SlideshowController to alert GameManager of updates
    public void SlideChanged(Slide newSlide)
    {
        DataManager.Instance.LogEvent("Displaying " + newSlide.name + " slide");
    }

    public void ModeFinished(ModeData data)
    {
        data.mode = modeOrder[curState];

        // Collect the data from the completed mode
        // TODO: make this cleaner
        switch( data.mode)
        {
            case GameModes.VR:
                vrData = data;
                break;
            case GameModes.Hybrid:
                hybridData = data;
                break;
            case GameModes.Desktop:
                desktopData = data;
                break;
        }

        // Advance to the next mode
        curState++;
        if(curState >= 3)
        {
            DataManager.Instance.LogEvent("Game Finished");
            DataManager.Instance.LogResultData(vrData, hybridData, desktopData);
        } 
        else
        {
            DataManager.Instance.LogEvent("Started " + modeOrder[curState].ToString() + " Mode");
            InitializeMode(curState);
        }
    }

}
