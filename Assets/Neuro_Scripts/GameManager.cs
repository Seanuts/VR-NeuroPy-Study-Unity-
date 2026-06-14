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

    [SerializeField] SlideshowController vrSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;
    // TODO: figure this out
    [SerializeField] GameObject interactiveBrain;

    // instruction slides
    [SerializeField] Slide vrIntroSlide;
    [SerializeField] Slide desktopIntroSlide;
    [SerializeField] Slide hybridIntroSlide;

    // game sequence
    [SerializeField] List<SlideBlock> blockOrder;
    List<GameModes> modeOrder = new List<GameModes> { GameModes.VR, GameModes.Desktop, GameModes.Hybrid };
    int curState;

    void Awake()
    {
        // load as singleton
        if( GameManager.Instance != null && GameManager.Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        GameManager.Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // randomize order
        blockOrder = blockOrder.OrderBy(x => UnityEngine.Random.value).ToList();
        modeOrder = modeOrder.OrderBy(x => UnityEngine.Random.value).ToList();
    }

    void Start()
    {
        // start the first mode
        curState = 0;
        InitializeMode(curState);
    }

    void InitializeMode(int state)
    {
        GameModes curMode = modeOrder[state];
        SlideBlock curBlock = blockOrder[state];
        switch (curMode)
        {
            case GameModes.Desktop:
                desktopSlideshow.gameObject.SetActive(true);
                vrSlideshow.gameObject.SetActive(false);
                interactiveBrain.gameObject.SetActive(false);
                desktopSlideshow.StartSlideshow(desktopIntroSlide, curBlock);
                break;
            
            case GameModes.Hybrid:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                interactiveBrain.gameObject.SetActive(false);
                vrSlideshow.StartSlideshow(hybridIntroSlide, curBlock);
                break;
            
            case GameModes.VR:
                desktopSlideshow.gameObject.SetActive(false);
                vrSlideshow.gameObject.SetActive(true);
                interactiveBrain.gameObject.SetActive(true);
                vrSlideshow.StartSlideshow(vrIntroSlide, curBlock);
                break;
        }
    }

    public void ModeFinished(ModeData data)
    {
        // TODO: integrate data collection
        data.mode = modeOrder[curState];
        Debug.Log($"{data.mode.ToString()} RESULT:\n\tcorrect: {data.questionsCorrect}\n\tincorrect: {data.questionsWrong}"); // DEBUG

        // advance to the next mode
        curState++;
        if(curState >= 3)
        {
            // TODO: do something when game finishes
            Debug.Log("GAME FINISHED!"); // DEBUG
        } 
        else
        {
            InitializeMode(curState);
        }
    }


    // Update is called once per frame
    //void Update()
    //{
    //}
}
