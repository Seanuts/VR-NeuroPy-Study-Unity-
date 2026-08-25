using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

//playtest_location for playtest removal
public enum GameModes
{
    INSTRUCTIONS,
    VIRTUAL_3D,
    MIXED_3D,
    MIXED_2D
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] GameObject player;
    [SerializeField] BrainHighlighter brain;

    [SerializeField] GameObject simPlayer;
    [SerializeField] GameObject simScreen;

    [SerializeField] SlideshowController gameSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;
    SlideshowController curSlideshow;

    // Game sequence
    [SerializeField] SlideBlock instructionBlock;
    [SerializeField] List<SlideBlock> learningBlocks;

    List<SlideBlock> blockOrder;
    List<GameModes> modeOrder;
    int curModeIndex;
    bool isFinishingMode;
    bool modeFinishQueued;
    readonly List<Behaviour> xrDeviceSimulators = new List<Behaviour>();
    readonly List<Behaviour> xrRayInteractors = new List<Behaviour>();


    // Triggered by SlideshowController to alert GameManager of updates
    public void SlideChanged(Slide newSlide)
    {
        GameModes mode = CurrentMode();
        //DataManager.Instance.LogEvent(newSlide.name);
        // Update interactive brain for modes that utilize it
        if (mode == GameModes.VIRTUAL_3D || mode == GameModes.MIXED_3D)
        {
            brain.LoadSlide(newSlide);
        }
    }

    // Triggered by SlideshowController to alert GameManager of mode finishing
    public void ModeFinished()
    {
        if (isFinishingMode || modeFinishQueued || modeOrder == null ||
            curModeIndex < 0 || curModeIndex >= modeOrder.Count)
            return;

        // Let the current button/timer event finish before changing Canvas
        // layers, camera masks, or active scene objects.
        modeFinishQueued = true;
        StartCoroutine(FinishModeNextFrame());
    }

    IEnumerator FinishModeNextFrame()
    {
        yield return null;
        modeFinishQueued = false;

        if (isFinishingMode || modeOrder == null ||
            curModeIndex < 0 || curModeIndex >= modeOrder.Count)
            yield break;

        isFinishingMode = true;
        // Advance to the next mode
        curModeIndex++;
        if (curModeIndex >= modeOrder.Count)
        {
            GameFinished();
        }
        else
        {
            InitializeMode(curModeIndex);
            isFinishingMode = false;
        }
    }

    // Fetch current game mode
    public GameModes CurrentMode()
    {
        return modeOrder[curModeIndex];
    }

    public bool UsesExternalMouse{ //use the physical mouse only for mixed2D slideshow UI
        get{
            return modeOrder != null &&
                   curModeIndex >= 0 &&
                   curModeIndex < modeOrder.Count &&
                   modeOrder[curModeIndex] == GameModes.MIXED_2D;
        }
    }

    public SlideshowController CurrentSlideshow => curSlideshow;

    void Awake()
    {
        // Load as singleton
        if (GameManager.Instance != null && GameManager.Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        GameManager.Instance = this;

        CacheXRBehaviours();

        // Randomize learning blocks
        learningBlocks = learningBlocks.OrderBy(x => UnityEngine.Random.value).ToList();
        // Create block sequence
        blockOrder = new List<SlideBlock>{ instructionBlock };
        blockOrder.AddRange(learningBlocks);

        // Randomize playable game modes
        List<GameModes> randomModes = new List<GameModes> { 
            GameModes.VIRTUAL_3D, GameModes.MIXED_3D, GameModes.MIXED_2D 
            //GameModes.MIXED_3D //playtest_location

        };
        randomModes = randomModes.OrderBy(x => UnityEngine.Random.value).ToList();
        // Create mode sequence (force start w/ instructions)
        modeOrder = new List<GameModes> { GameModes.INSTRUCTIONS };
        modeOrder.AddRange(randomModes);
    }

    void Start()
    {
        // Start the first mode
        curModeIndex = 0;
        InitializeMode(curModeIndex);
        // curModeIndex = modeOrder.IndexOf(GameModes.MIXED_3D); //playtest
        // InitializeMode(curModeIndex);
    }

    void InitializeMode(int state)
    {
        GameModes curMode = modeOrder[state];
        SlideBlock curBlock = blockOrder[state];
        //DataManager.Instance.LogEvent(curMode.ToString());

        ConfigureXRInputForMode(curMode);

        QuestProPassthrough.SetEnabled(
            curMode == GameModes.MIXED_3D || curMode == GameModes.MIXED_2D,
            player,
            simPlayer,
            simScreen,
            curMode == GameModes.MIXED_2D ? desktopSlideshow.gameObject : gameSlideshow.gameObject,
            curMode == GameModes.MIXED_3D);

        // Switch environment to match current mode
        switch (curMode)
        {
            case GameModes.INSTRUCTIONS:
                InitVirtual3D();
                break;
            case GameModes.VIRTUAL_3D:
                InitVirtual3D();
                break;
            case GameModes.MIXED_3D:
                InitMixed3D();
                break;
            case GameModes.MIXED_2D:
                InitMixed2D();
                break;
        }

        // Play selected block for this mode
        curSlideshow.StartSlideshow(state, curBlock);
    }

    void ConfigureXRInputForMode(GameModes mode)
    {
        bool enableXRInput = mode != GameModes.MIXED_2D;

        SetXRBehavioursEnabled(xrDeviceSimulators, enableXRInput);
        SetXRBehavioursEnabled(xrRayInteractors, enableXRInput);
    }

    void CacheXRBehaviours()
    {
        Behaviour[] behaviours = FindObjectsOfType<Behaviour>(true);
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (HasBaseTypeNamed(behaviour, "XRDeviceSimulator"))
                xrDeviceSimulators.Add(behaviour);
            else if (HasBaseTypeNamed(behaviour, "XRRayInteractor"))
                xrRayInteractors.Add(behaviour);
        }
    }

    void SetXRBehavioursEnabled(List<Behaviour> behaviours, bool enabled)
    {
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }

    bool HasBaseTypeNamed(Behaviour behaviour, string typeName)
    {
        for (Type type = behaviour.GetType(); type != null; type = type.BaseType)
        {
            if (type.Name == typeName)
                return true;
        }

        return false;
    }

    void InitVirtual3D()
    {
        player.transform.localPosition = new Vector3(0, -1.5f, -6);
        player.transform.localRotation = Quaternion.identity;
        
        // Disable brain for instructions
        if ( CurrentMode() == GameModes.INSTRUCTIONS)
        {
            brain.gameObject.SetActive(false);
        }
        else
        {
            brain.gameObject.SetActive(true);
        }

        simPlayer.SetActive(false);
        simScreen.SetActive(false);

        gameSlideshow.gameObject.SetActive(true);
        desktopSlideshow.gameObject.SetActive(false);
        
        curSlideshow = gameSlideshow;
    }

    void InitMixed3D()
    {
        player.transform.localPosition = new Vector3(16.5f, 0, -6);
        player.transform.localRotation = Quaternion.identity;
        brain.gameObject.SetActive(true);

        simPlayer.SetActive(true);
        simScreen.SetActive(true);

        gameSlideshow.gameObject.SetActive(true);
        desktopSlideshow.gameObject.SetActive(false);

        curSlideshow = gameSlideshow;
    }

    void InitMixed2D()
    {
        player.transform.localPosition = new Vector3(16.5f, 0, -6);
        player.transform.localRotation = Quaternion.identity;
        brain.gameObject.SetActive(false);

        simPlayer.SetActive(true);
        simScreen.SetActive(false);

        gameSlideshow.gameObject.SetActive(false);
        desktopSlideshow.gameObject.SetActive(true);

        curSlideshow = desktopSlideshow;
    }


    async void GameFinished()
    {
        // Record final event
        //DataManager.Instance.LogEvent("FINISHED");
        // Load lobby scene again
        await SceneManager.LoadSceneAsync("ThankYou");
    }

}
