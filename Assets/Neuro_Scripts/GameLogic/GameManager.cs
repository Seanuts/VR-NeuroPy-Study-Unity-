using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;

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

    [Header("Mixed mode virtual screen border")]
    [SerializeField, Min(0f)] float mixedModeScreenBorderThickness = 6f; //border thickness
    [SerializeField] string mixedModeScreenBorderHexColor = "66CCFF"; //toggle border color

    [SerializeField] SlideshowController gameSlideshow;
    [SerializeField] SlideshowController desktopSlideshow;
    SlideshowController curSlideshow;

    [Header("Learning block transition")]
    [SerializeField, Min(0f)] float learningBlockFadeOutTime = 1f;
    [SerializeField, Min(0f)] float learningBlockFadeInTime = 1f;
    LearningBlockFade learningBlockFade;

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
    GameObject mixed3DScreenBorder;
    GameObject mixed2DScreenBorder;


    // Triggered by SlideshowController to alert GameManager of updates
    public void SlideChanged(Slide newSlide)
    {
        GameModes mode = CurrentMode();
        DataManager.Instance.LogEvent(newSlide.name);
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
        bool isLearningBlockTransition = IsLearningBlockTransition();
        if (isLearningBlockTransition)
            yield return FadeLearningBlockTo(1f, learningBlockFadeOutTime);

        // Advance to the next mode
        curModeIndex++;
        if (curModeIndex >= modeOrder.Count)
        {
            GameFinished();
        }
        else
        {
            InitializeMode(curModeIndex);
            if (isLearningBlockTransition)
                yield return FadeLearningBlockTo(0f, learningBlockFadeInTime);

            isFinishingMode = false;
        }
    }

    bool IsLearningBlockTransition(){
        return curModeIndex >= 0 && curModeIndex + 1 < modeOrder.Count;
    }

    IEnumerator FadeLearningBlockTo(float targetAlpha, float duration)
    {
        LearningBlockFade fade = GetLearningBlockFade();
        if (fade == null)
            yield break;

        float startAlpha = fade.Alpha;
        if (duration <= 0f)
        {
            fade.SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration));
            yield return null;
        }

        fade.SetAlpha(targetAlpha);
    }

    LearningBlockFade GetLearningBlockFade()
    {
        if (learningBlockFade != null)
            return learningBlockFade;

        Camera playerCamera = player != null
            ? player.GetComponentInChildren<Camera>(true)
            : null;
        if (playerCamera == null)
        {
            Debug.LogWarning("Could not create learning block fade: no player camera was found.");
            return null;
        }

        GameObject fadeObject = new GameObject("LearningBlockFade", typeof(RectTransform));
        fadeObject.transform.SetParent(playerCamera.transform, false);
        learningBlockFade = fadeObject.AddComponent<LearningBlockFade>();
        learningBlockFade.Initialize(playerCamera);
        return learningBlockFade;
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
    }

    void InitializeMode(int state)
    {
        GameModes curMode = modeOrder[state];
        SlideBlock curBlock = blockOrder[state];
        DataManager.Instance.LogEvent(curMode.ToString());

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

        UpdateMixedModeScreenBorder(curMode);

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
        player.transform.localPosition = new Vector3(0, -1.5f, -6.5f);
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

    void UpdateMixedModeScreenBorder(GameModes mode)
    {
        SetScreenBorderVisible(mixed3DScreenBorder, false);
        SetScreenBorderVisible(mixed2DScreenBorder, false);

        if (mode == GameModes.MIXED_3D)
        {
            RectTransform screen = simScreen != null ? simScreen.transform as RectTransform : null;
            mixed3DScreenBorder = EnsureScreenBorder(screen, mixed3DScreenBorder);
            SetScreenBorderVisible(mixed3DScreenBorder, true);
        }
        else if (mode == GameModes.MIXED_2D)
        {
            RectTransform screen = desktopSlideshow != null ? desktopSlideshow.InteractionRect : null;
            mixed2DScreenBorder = EnsureScreenBorder(screen, mixed2DScreenBorder);
            SetScreenBorderVisible(mixed2DScreenBorder, true);
        }
    }

    GameObject EnsureScreenBorder(RectTransform screen, GameObject border)
    {
        if (screen == null)
            return null;

        if (border == null)
        {
            border = new GameObject("MixedModeScreenBorder", typeof(RectTransform));
            CreateScreenBorderBar(border.transform, "Top");
            CreateScreenBorderBar(border.transform, "Bottom");
            CreateScreenBorderBar(border.transform, "Left");
            CreateScreenBorderBar(border.transform, "Right");
        }

        RectTransform borderRect = border.transform as RectTransform;
        borderRect.SetParent(screen, false);
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.SetAsLastSibling();

        Color borderColor = ParseScreenBorderColor();
        ConfigureScreenBorderBar(border.transform.Find("Top"), borderColor, true);
        ConfigureScreenBorderBar(border.transform.Find("Bottom"), borderColor, true);
        ConfigureScreenBorderBar(border.transform.Find("Left"), borderColor, false);
        ConfigureScreenBorderBar(border.transform.Find("Right"), borderColor, false);
        return border;
    }

    void CreateScreenBorderBar(Transform parent, string barName)
    {
        GameObject bar = new GameObject(barName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().raycastTarget = false;
    }

    void ConfigureScreenBorderBar(Transform barTransform, Color color, bool horizontal)
    {
        if (barTransform == null)
            return;

        RectTransform bar = barTransform as RectTransform;
        Image image = bar.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        float thickness = Mathf.Max(0f, mixedModeScreenBorderThickness);
        if (horizontal)
        {
            bool top = bar.name == "Top";
            bar.anchorMin = new Vector2(0f, top ? 1f : 0f);
            bar.anchorMax = new Vector2(1f, top ? 1f : 0f);
            bar.pivot = new Vector2(0.5f, top ? 1f : 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(thickness * 2f, thickness);
        }
        else
        {
            bool left = bar.name == "Left";
            bar.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            bar.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            bar.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(thickness, thickness * 2f);
        }

        bar.gameObject.SetActive(thickness > 0f);
    }

    Color ParseScreenBorderColor()
    {
        string hex = mixedModeScreenBorderHexColor == null
            ? string.Empty
            : mixedModeScreenBorderHexColor.Trim();
        if (!hex.StartsWith("#"))
            hex = "#" + hex;

        Color color;
        if (hex.Length == 7 && ColorUtility.TryParseHtmlString(hex, out color))
        {
            color.a = 1f;
            return color;
        }

        //Debug.LogWarning("Mixed mode screen border color must be a 6 digit hex code. Using #66CCFF instead.");
        return new Color32(102, 204, 255, 255);
    }

    void SetScreenBorderVisible(GameObject border, bool visible)
    {
        if (border != null)
            border.SetActive(visible && mixedModeScreenBorderThickness > 0f);
    }


    async void GameFinished()
    {
        // Record final event
        DataManager.Instance.LogEvent("FINISHED");
        // Load lobby scene again
        await SceneManager.LoadSceneAsync("ThankYou");
    }

}
