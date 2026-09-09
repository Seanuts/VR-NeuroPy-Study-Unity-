using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SimulatedPlayer : MonoBehaviour
{
    public static Camera ActiveSimulatedCamera { get; private set; }

    [SerializeField] Camera simCamera;
    [SerializeField] RectTransform simScreen;

    [Header("Mixed3D mouse pointer")]
    [SerializeField, Min(1f)] float mixed3DPointerSize = 18f;
    [SerializeField, Min(0f)] float mixed3DPointerOutlineThickness = 1f;
    [SerializeField, Min(0.01f)] float mixed3DMouseSensitivity = 2f;
    [SerializeField] Color mixed3DPointerColor = Color.white;
    [Tooltip("Fallback orientation used only when GameScreen cannot be found.")]
    [SerializeField] Vector3 mixed3DCameraLocalEulerAngles = Vector3.zero;

    [Header("Virtual pointer")]
    [SerializeField] float pointerSmoothing = 18f;
    [SerializeField] float pointerDeadzonePixels = 1.5f;

    // Control variables for desktop/editor camera fallback.
    const float mouseSensitivity = .25f;
    const float minPitch = -80f;
    const float maxPitch = 80f;

    float curYaw;
    float curPitch;
    RectTransform mixed3DPointer;
    Vector2 mixed3DPointerViewport = new Vector2(.5f, .5f);
    bool mixed3DMouseMode;
    Transform mixed3DCameraTarget;
    GraphicRaycaster simulatedCanvasRaycaster;
    Canvas simulatedCanvas;
    GraphicRaycaster externalMouseRaycaster;
    Canvas externalMouseCanvas;
    Canvas externalMouseCameraCanvas;
    Camera externalMouseOriginalCamera;
    RectTransform externalMouseBounds;
    RectTransform mouseCursor;
    Sprite mouseArrowSprite;
    Vector2 externalMouseBoundsPosition;
    bool hasExternalMouseBoundsPosition;

    float externalMouseSensitivity = 2f; //mouse sensitivity *2 since prev was too sluggish
    GameObject virtualHoveredObject;
    PointerEventData virtualPointerData;
    RaycastResult virtualRaycast;

    // VR Pointer Tracking
    Vector2 lastPointerViewport;
    Vector2 smoothedPointerViewport;
    bool hasLastPointerViewport;
    bool hasSmoothedPointerViewport;
    bool isPointerOverScreen;

    // Interaction Tracking
    bool hasVirtualRaycast;
    bool hasVirtualPosition;
    bool virtualPointerPressed;
    GameObject virtualPressedObject;
    bool suppressNextPointerClick;

    // External mouse interaction for the flat mixed2D slideshow.
    PointerEventData mousePointerData;
    GameObject mouseHoveredObject;
    GameObject mousePressedObject;
    RaycastResult mouseRaycast;
    bool mousePointerPressed;
    bool externalMouseMode;
    readonly List<BaseInputModule> disabledInputModules = new List<BaseInputModule>();
    readonly List<bool> disabledInputModuleStates = new List<bool>();

    void Awake()
    {
        if (simCamera == null)
            simCamera = GetComponent<Camera>();

        if (simScreen == null)
            simScreen = FindSiblingRectTransform("SimScreen");

        if (simScreen != null)
        {
            RawImage rawImage = simScreen.GetComponentInChildren<RawImage>(true);
            if (rawImage != null)
                rawImage.raycastTarget = false;

            ScreenPointerRelay relay = simScreen.GetComponent<ScreenPointerRelay>();
            if (relay == null)
                relay = simScreen.gameObject.AddComponent<ScreenPointerRelay>();
            relay.Initialize(this);

            CreateMixed3DPointer();
            SetMixed3DPointerVisible(false);
        }

        Transform gameScreen = FindSiblingTransform("GameScreen");
        if (gameScreen != null)
        {
            mixed3DCameraTarget = gameScreen;
            simulatedCanvas = gameScreen.GetComponent<Canvas>();
            simulatedCanvasRaycaster = gameScreen.GetComponent<GraphicRaycaster>();
            if (simulatedCanvas != null)
            {
                simulatedCanvas.worldCamera = simCamera;
                if (simulatedCanvasRaycaster == null)
                    simulatedCanvasRaycaster = simulatedCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }

    void OnEnable()
    {
        if (simCamera == null)
            simCamera = GetComponent<Camera>();

        ActiveSimulatedCamera = simCamera;

    }

    void OnDisable()
    {
        SetExternalMouseMode(false);
        SetMixed3DMouseMode(false);
        ReleaseVirtualPointer(false);
        ClearVirtualHover();

        if (ActiveSimulatedCamera == simCamera)
            ActiveSimulatedCamera = null;

        // Hand the OS cursor back unlocked and visible. The in-game modes keep it
        // Locked/hidden for mouse-look; leaving it that way on teardown breaks the
        // next scene's UI (e.g. ThankYou buttons get no movable pointer).
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        Transform targetTransform = simCamera != null ? simCamera.transform : transform;
        curYaw = targetTransform.localEulerAngles.y;
        curPitch = NormalizeAngle(targetTransform.localEulerAngles.x);
    }

    void Update()
    {
        bool shouldUseExternalMouse = GameManager.Instance != null &&
                                      GameManager.Instance.UsesExternalMouse;
        SetExternalMouseMode(shouldUseExternalMouse);
        bool isMixed3D = IsMixed3DMode();
        SetMixed3DMouseMode(isMixed3D);

        // Mixed2D is the only mode that drives the real system pointer; hide the
        // OS cursor in every other mode (desktop mouse-look and Mixed3D alike).
        if (!externalMouseMode)
            HideDesktopCursor();

        if (externalMouseMode)
        {
            ProcessExternalMousePointer();
            return;
        }

        if (isMixed3D)
        {
            LockMixed3DCamera();
            UpdateMixed3DMouseInput();
            return;
        }

        // If the XR pointer is actively driving the view, do NOT also process desktop mouse delta
        if (isPointerOverScreen || Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        if (mouseDelta != Vector2.zero)
        {
            curYaw += mouseDelta.x * mouseSensitivity;
            curPitch -= mouseDelta.y * mouseSensitivity;
            curPitch = Mathf.Clamp(curPitch, minPitch, maxPitch);
            ApplyCameraRotation();
        }
    }

    void SetExternalMouseMode(bool enabled)
    {
        if (externalMouseMode == enabled)
            return;

        externalMouseMode = enabled;
        ReleaseMousePointer(false);
        ClearMouseHover();
        hasExternalMouseBoundsPosition = false;

        if (enabled)
        {
            ConfigureExternalMouseCanvas();

            // Lock+hide the hardware cursor and drive everything from mouse delta
            // (same as Mixed3D). The game draws its own arrow, so the real OS
            // cursor is redundant and would otherwise trail out of the Game view
            // into the Inspector while testing in the editor, starving the delta.
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                BaseInputModule[] inputModules = eventSystem.GetComponents<BaseInputModule>();
                foreach (BaseInputModule inputModule in inputModules)
                {
                    disabledInputModules.Add(inputModule);
                    disabledInputModuleStates.Add(inputModule.enabled);
                    inputModule.enabled = false;
                }
            }
        }
        else
        {
            RestoreExternalMouseCanvas();

            for (int i = 0; i < disabledInputModules.Count; i++)
            {
                if (disabledInputModules[i] != null)
                    disabledInputModules[i].enabled = disabledInputModuleStates[i];
            }

            disabledInputModules.Clear();
            disabledInputModuleStates.Clear();
            HideDesktopCursor();
        }
    }

    void HideDesktopCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        SetMouseCursorVisible(false);
    }

    void ProcessExternalMousePointer()
    {
        ConfigureExternalMouseCanvas();

        if (externalMouseRaycaster == null || externalMouseCanvas == null ||
            EventSystem.current == null || Mouse.current == null)
        {
            ReleaseMousePointer(false);
            ClearMouseHover();
            SetMouseCursorVisible(false);
            return;
        }

        if (mousePointerData == null)
            mousePointerData = new PointerEventData(EventSystem.current);

        mousePointerData.pointerId = -1;

        if (!hasExternalMouseBoundsPosition)
        {
            externalMouseBoundsPosition = GetClampedBoundsCenter();
            hasExternalMouseBoundsPosition = true;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Vector2 boundsSize = externalMouseBounds.rect.size;
        externalMouseBoundsPosition += Vector2.Scale(mouseDelta / screenSize, boundsSize) * externalMouseSensitivity;
        externalMouseBoundsPosition = ClampBoundsPosition(externalMouseBoundsPosition);

        Vector2 mousePosition = BoundsLocalToCanvasPosition(externalMouseBoundsPosition);
        mousePointerData.delta = mousePosition - mousePointerData.position;
        mousePointerData.position = mousePosition;
        mousePointerData.scrollDelta = Mouse.current.scroll.ReadValue();
        mousePointerData.button = PointerEventData.InputButton.Left;

        UpdateMouseCursor(externalMouseBoundsPosition);

        List<RaycastResult> results = new List<RaycastResult>();
        externalMouseRaycaster.Raycast(mousePointerData, results);
        mouseRaycast = results.Count > 0 ? results[0] : default;
        mousePointerData.pointerCurrentRaycast = mouseRaycast;

        GameObject nextHoveredObject = results.Count > 0 ? results[0].gameObject : null;
        if (nextHoveredObject != mouseHoveredObject)
        {
            mousePointerData.pointerEnter = mouseHoveredObject;
            if (mouseHoveredObject != null)
                ExecuteEvents.ExecuteHierarchy(mouseHoveredObject, mousePointerData, ExecuteEvents.pointerExitHandler);

            mouseHoveredObject = nextHoveredObject;
            mousePointerData.pointerEnter = mouseHoveredObject;

            if (mouseHoveredObject != null)
                ExecuteEvents.ExecuteHierarchy(mouseHoveredObject, mousePointerData, ExecuteEvents.pointerEnterHandler);
        }

        if (mouseHoveredObject != null)
            ExecuteEvents.ExecuteHierarchy(mouseHoveredObject, mousePointerData, ExecuteEvents.pointerMoveHandler);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            PressMousePointer();

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            ReleaseMousePointer(true);
    }

    void PressMousePointer()
    {
        if (mousePointerPressed || mouseHoveredObject == null)
            return;

        mousePointerData.pointerPressRaycast = mouseRaycast;
        mousePointerData.pressPosition = mousePointerData.position;
        mousePointerData.clickCount = 1;
        mousePointerData.clickTime = Time.unscaledTime;
        mousePointerData.eligibleForClick = true;
        mousePointerData.button = PointerEventData.InputButton.Left;

        mousePressedObject = mouseHoveredObject;
        mousePointerPressed = true;
        mousePointerData.pointerPress = ExecuteEvents.ExecuteHierarchy(
            mousePressedObject, mousePointerData, ExecuteEvents.pointerDownHandler);
        mousePointerData.rawPointerPress = mousePressedObject;
    }

    void ReleaseMousePointer(bool sendClick)
    {
        if (!mousePointerPressed || mousePointerData == null)
            return;

        GameObject pressedObject = mousePressedObject;
        bool releaseOverPressedObject = pressedObject != null &&
            (mouseHoveredObject == pressedObject ||
             (mouseHoveredObject != null &&
              (mouseHoveredObject.transform.IsChildOf(pressedObject.transform) ||
               pressedObject.transform.IsChildOf(mouseHoveredObject.transform))));

        mousePointerData.pointerCurrentRaycast = mouseRaycast;
        if (pressedObject != null)
        {
            ExecuteEvents.ExecuteHierarchy(pressedObject, mousePointerData, ExecuteEvents.pointerUpHandler);

            if (sendClick && mousePointerData.eligibleForClick && releaseOverPressedObject)
                ExecuteEvents.ExecuteHierarchy(pressedObject, mousePointerData, ExecuteEvents.pointerClickHandler);
        }

        mousePointerData.pointerPress = null;
        mousePointerData.rawPointerPress = null;
        mousePointerData.eligibleForClick = false;
        mousePressedObject = null;
        mousePointerPressed = false;
    }

    void ClearMouseHover()
    {
        if (mousePointerData != null)
            mousePointerData.pointerEnter = mouseHoveredObject;
        if (mouseHoveredObject != null && mousePointerData != null)
            ExecuteEvents.ExecuteHierarchy(mouseHoveredObject, mousePointerData, ExecuteEvents.pointerExitHandler);

        mouseHoveredObject = null;
        if (mousePointerData != null)
            mousePointerData.pointerEnter = null;
        mouseRaycast = default;
    }

    void ConfigureExternalMouseCanvas()
    {
        Canvas targetCanvas = null;
        if (GameManager.Instance != null && GameManager.Instance.CurrentSlideshow != null)
            targetCanvas = GameManager.Instance.CurrentSlideshow.InteractionCanvas;

        if (targetCanvas == null)
            targetCanvas = simulatedCanvas;

        if (targetCanvas == null)
        {
            RestoreExternalMouseCanvas();
            return;
        }

        SlideshowController currentSlideshow = GameManager.Instance != null
            ? GameManager.Instance.CurrentSlideshow
            : null;
        externalMouseBounds = currentSlideshow != null
            ? currentSlideshow.InteractionRect
            : targetCanvas.transform as RectTransform;

        if (externalMouseCanvas != targetCanvas)
        {
            RestoreExternalMouseCanvas();
            externalMouseCanvas = targetCanvas;
            Camera mouseCamera = GetExternalMouseCamera(externalMouseCanvas);
            if (externalMouseCanvas.renderMode == RenderMode.WorldSpace)
            {
                externalMouseCameraCanvas = externalMouseCanvas;
                externalMouseOriginalCamera = externalMouseCanvas.worldCamera;
                externalMouseCanvas.worldCamera = mouseCamera;
            }
            else if (externalMouseCanvas.worldCamera == null)
            {
                externalMouseCanvas.worldCamera = mouseCamera;
            }
            externalMouseRaycaster = externalMouseCanvas.GetComponent<GraphicRaycaster>();
            if (externalMouseRaycaster == null)
                externalMouseRaycaster = externalMouseCanvas.gameObject.AddComponent<GraphicRaycaster>();

            CreateMouseCursor();
        }
    }

    void RestoreExternalMouseCanvas()
    {
        if (externalMouseCameraCanvas != null)
            externalMouseCameraCanvas.worldCamera = externalMouseOriginalCamera;

        externalMouseCameraCanvas = null;
        externalMouseOriginalCamera = null;
        externalMouseCanvas = null;
        externalMouseRaycaster = null;
        externalMouseBounds = null;
        hasExternalMouseBoundsPosition = false;
    }

    Vector2 GetClampedBoundsCenter()
    {
        Rect boundsRect = externalMouseBounds.rect;
        return ClampBoundsPosition(boundsRect.center);
    }

    Vector2 ClampBoundsPosition(Vector2 boundsPosition)
    {
        Rect boundsRect = externalMouseBounds.rect;
        float cursorWidth = mouseCursor != null ? mouseCursor.rect.width : 0f;
        float cursorHeight = mouseCursor != null ? mouseCursor.rect.height : 0f;
        float minX = boundsRect.xMin;
        float maxX = Mathf.Max(minX, boundsRect.xMax - cursorWidth);
        float minY = Mathf.Min(boundsRect.yMax, boundsRect.yMin + cursorHeight);
        float maxY = boundsRect.yMax;

        boundsPosition.x = Mathf.Clamp(boundsPosition.x, minX, maxX);
        boundsPosition.y = Mathf.Clamp(boundsPosition.y, minY, maxY);
        return boundsPosition;
    }

    Vector2 BoundsLocalToCanvasPosition(Vector2 boundsPosition)
    {
        Camera canvasCamera = GetExternalMouseCamera(externalMouseCanvas);
        Vector3 worldPosition = externalMouseBounds.TransformPoint(boundsPosition);
        return RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);
    }

    void CreateMouseCursor()
    { //create mouse cursor
        if (mouseCursor == null)
        {
            mouseCursor = new GameObject("DesktopMouseCursor", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            mouseCursor.anchorMin = Vector2.zero;
            mouseCursor.anchorMax = Vector2.zero;
            mouseCursor.pivot = new Vector2(0f, 1f);
            mouseCursor.sizeDelta = new Vector2(18f, 18f);

            Image image = mouseCursor.GetComponent<Image>();
            image.sprite = GetMouseArrowSprite();
            image.color = Color.white;
            image.raycastTarget = false;

            Outline outline = mouseCursor.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        mouseCursor.SetParent(externalMouseBounds != null ? externalMouseBounds : externalMouseCanvas.transform, false);
        mouseCursor.SetAsLastSibling();
    }

    Sprite GetMouseArrowSprite()
    { //arrow shared by the separate Mixed2D and Mixed3D cursor objects
        if (mouseArrowSprite != null)
            return mouseArrowSprite;

        const int size = 18;
        const int antialiasSamples = 4;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float scale = size / 24f;
        Vector2[] arrow =
        {
            new Vector2(1f, 23f) * scale,
            new Vector2(1f, 3f) * scale,
            new Vector2(7f, 9f) * scale,
            new Vector2(12f, 2f) * scale,
            new Vector2(16f, 4f) * scale,
            new Vector2(11f, 12f) * scale,
            new Vector2(20f, 12f) * scale
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int coveredSamples = 0;
                for (int sampleY = 0; sampleY < antialiasSamples; sampleY++)
                {
                    for (int sampleX = 0; sampleX < antialiasSamples; sampleX++)
                    {
                        Vector2 samplePosition = new Vector2(
                            x + (sampleX + .5f) / antialiasSamples,
                            y + (sampleY + .5f) / antialiasSamples);
                        if (PointInsidePolygon(samplePosition, arrow))
                            coveredSamples++;
                    }
                }

                byte alpha = (byte)Mathf.RoundToInt(
                    coveredSamples * 255f / (antialiasSamples * antialiasSamples));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        mouseArrowSprite = Sprite.Create(
            texture, new Rect(0f, 0f, size, size), new Vector2(0f, 1f), size);
        return mouseArrowSprite;
    }

    bool PointInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
            if (crosses && point.x < (polygon[j].x - polygon[i].x) *
                (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    void UpdateMouseCursor(Vector2 boundsPosition)
    {
        if (mouseCursor == null || externalMouseBounds == null || externalMouseCanvas == null)
            return;

        mouseCursor.localPosition = new Vector3(boundsPosition.x, boundsPosition.y, 0f);
        SetMouseCursorVisible(true);
    }

    void SetMouseCursorVisible(bool visible)
    {
        if (mouseCursor != null)
            mouseCursor.gameObject.SetActive(visible && externalMouseMode && externalMouseBounds != null);
    }

    Camera GetExternalMouseCamera(Canvas canvas)
    {
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera &&
            canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return Camera.main != null ? Camera.main : simCamera;
    }

    void CreateMixed3DPointer()
    {
        if (mixed3DPointer != null || simScreen == null)
            return;

        mixed3DPointer = new GameObject(
            "Mixed3DMouseCursor", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        mixed3DPointer.SetParent(simScreen, false);
        mixed3DPointer.anchorMin = new Vector2(.5f, .5f);
        mixed3DPointer.anchorMax = new Vector2(.5f, .5f);
        mixed3DPointer.pivot = new Vector2(0f, 1f);
        mixed3DPointer.sizeDelta = Vector2.one * mixed3DPointerSize;
        mixed3DPointer.SetAsLastSibling();

        Image image = mixed3DPointer.GetComponent<Image>();
        image.sprite = GetMouseArrowSprite();
        image.color = mixed3DPointerColor;
        image.raycastTarget = false;

        Outline outline = mixed3DPointer.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .95f);
        outline.effectDistance = new Vector2(
            mixed3DPointerOutlineThickness, -mixed3DPointerOutlineThickness);
        outline.useGraphicAlpha = true;
    }

    void ApplyCameraRotation()
    {
        Quaternion targetRotation = Quaternion.Euler(curPitch, curYaw, 0f);
        if (simCamera != null)
            simCamera.transform.localRotation = targetRotation;
        else
            transform.localRotation = targetRotation;
    }

    Transform FindSiblingTransform(string objectName)
    {
        Transform parent = transform.parent;
        if (parent == null) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;

        return null;
    }

    RectTransform FindSiblingRectTransform(string objectName)
    {
        return FindSiblingTransform(objectName) as RectTransform;
    }

    void SetMixed3DMouseMode(bool enabled)
    {
        if (mixed3DMouseMode == enabled)
            return;

        mixed3DMouseMode = enabled;
        ReleaseVirtualPointer(false);
        ClearVirtualHover();
        ResetVirtualPointerTracking();

        if (enabled)
        {
            mixed3DPointerViewport = ClampVirtualPointerViewport(new Vector2(.5f, .5f));
            HideDesktopCursor();
            LockMixed3DCamera();
            UpdateMixed3DPointerVisual();
        }
        else
        {
            SetMixed3DPointerVisible(false);
        }
    }

    bool IsMixed3DMode()
    {
        // GameManager enables SimScreen only for the Mixed_3D block.
        return simScreen != null && simScreen.gameObject.activeInHierarchy;
    }

    void UpdateMixed3DMouseInput()
    {
        if (Mouse.current == null)
            return;

        isPointerOverScreen = false;
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        mixed3DPointerViewport += Vector2.Scale(mouseDelta / screenSize, Vector2.one) *
            mixed3DMouseSensitivity;
        mixed3DPointerViewport = ClampVirtualPointerViewport(mixed3DPointerViewport);

        UpdateVirtualPointerAtViewport(mixed3DPointerViewport, null, null, false);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            PressVirtualPointer(null);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            ReleaseVirtualPointer(true);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            ResetMixed3DBrain();
    }

    void LockMixed3DCamera()
    {
        Transform cameraTransform = simCamera != null ? simCamera.transform : transform;
        if (mixed3DCameraTarget != null)
        {
            Vector3 forward = mixed3DCameraTarget.position - cameraTransform.position;
            if (forward.sqrMagnitude > Mathf.Epsilon)
            {
                cameraTransform.rotation = Quaternion.LookRotation(
                    forward.normalized, mixed3DCameraTarget.up);
                return;
            }
        }

        cameraTransform.localRotation = Quaternion.Euler(mixed3DCameraLocalEulerAngles);
    }

    void UpdateMixed3DPointerVisual()
    {
        if (mixed3DPointer == null || simScreen == null)
            return;

        Rect screenRect = simScreen.rect;
        mixed3DPointer.anchoredPosition = new Vector2(
            Mathf.Lerp(screenRect.xMin, screenRect.xMax, mixed3DPointerViewport.x),
            Mathf.Lerp(screenRect.yMin, screenRect.yMax, mixed3DPointerViewport.y));
        mixed3DPointer.SetAsLastSibling();
        SetMixed3DPointerVisible(true);
    }

    void SetMixed3DPointerVisible(bool visible)
    {
        if (mixed3DPointer != null)
        {
            mixed3DPointer.gameObject.SetActive(
                visible && mixed3DMouseMode && simScreen != null && simScreen.gameObject.activeInHierarchy);
        }
    }

    void UpdateVirtualPointer(Vector3 screenWorldPosition, PointerEventData sourceEvent)
    {
        if (!TryGetSimulatedViewport(screenWorldPosition, out Vector2 pointerViewport))
            return;

        if (simCamera == null)
            return;

        UpdateVirtualPointerAtViewport(pointerViewport, sourceEvent);
    }

    void UpdateVirtualPointerAtViewport(
        Vector2 pointerViewport,
        PointerEventData sourceEvent,
        Vector2? forcedPixelDelta = null,
        bool smoothPointer = true)
    {
        if (simCamera == null)
            return;

        pointerViewport = ClampVirtualPointerViewport(pointerViewport);

        EnsureVirtualPointerData(sourceEvent);
        if (virtualPointerData == null)
            return;

        // Controller rays benefit from smoothing. Physical mouse input should
        // track the on-screen cursor exactly without adding visual latency.
        if (!smoothPointer || !hasSmoothedPointerViewport)
        {
            smoothedPointerViewport = pointerViewport;
            hasSmoothedPointerViewport = true;
        }
        else
        {
            float smoothing = 1f - Mathf.Exp(-pointerSmoothing * Time.unscaledDeltaTime);
            smoothedPointerViewport = Vector2.Lerp(smoothedPointerViewport, pointerViewport, smoothing);
        }

        // --- 1. POINTER MOVEMENT AND DRAG DELTA ---
        Vector2 pointerDelta = Vector2.zero;
        if (hasLastPointerViewport)
        {
            pointerDelta = smoothedPointerViewport - lastPointerViewport;

            if (smoothPointer)
            {
                Vector2 deadzonePixelDelta = pointerDelta * new Vector2(
                    simCamera.targetTexture != null ? simCamera.targetTexture.width : simCamera.pixelWidth,
                    simCamera.targetTexture != null ? simCamera.targetTexture.height : simCamera.pixelHeight);

                if (deadzonePixelDelta.sqrMagnitude < pointerDeadzonePixels * pointerDeadzonePixels)
                    pointerDelta = Vector2.zero;

                // Ignore implausibly large controller-tracking jumps.
                if (pointerDelta.sqrMagnitude >= 0.01f)
                    pointerDelta = Vector2.zero;
            }
        }
        lastPointerViewport = smoothedPointerViewport;
        hasLastPointerViewport = true;

        // --- 2. POINTER RAYCAST INTERACTION ---
        Vector2 interactionViewport = smoothedPointerViewport;
        Vector2 virtualPosition = ViewportToPixelPosition(interactionViewport);

        virtualPointerData.position = virtualPosition;
        Vector2 pointerPixelDelta = pointerDelta * new Vector2(
            simCamera.targetTexture != null ? simCamera.targetTexture.width : simCamera.pixelWidth,
            simCamera.targetTexture != null ? simCamera.targetTexture.height : simCamera.pixelHeight);
        if (forcedPixelDelta.HasValue)
            pointerPixelDelta = forcedPixelDelta.Value;
        virtualPointerData.delta = hasVirtualPosition ? pointerPixelDelta : Vector2.zero;
        hasVirtualPosition = true;
        if (mixed3DMouseMode)
        {
            mixed3DPointerViewport = smoothedPointerViewport;
            UpdateMixed3DPointerVisual();
        }

        List<RaycastResult> results = new List<RaycastResult>();
        if (simulatedCanvasRaycaster != null)
            simulatedCanvasRaycaster.Raycast(virtualPointerData, results);
        hasVirtualRaycast = results.Count > 0;
        virtualRaycast = hasVirtualRaycast ? results[0] : default;

        Ray ray = simCamera.ViewportPointToRay(new Vector3(
            interactionViewport.x, interactionViewport.y, 0f));
        RaycastHit[] physicsHits = Physics.RaycastAll(ray, simCamera.farClipPlane);

        // Prefer a brain hit over a canvas hit so the debug sphere remains a
        // reliable target when UI graphics are layered over the simulated view.
        if (TryGetClosestBrainHit(physicsHits, out RaycastHit brainHit))
        {
            SetVirtualPhysicsRaycast(brainHit);
        }
        else if (!hasVirtualRaycast && TryGetClosestPhysicsHit(physicsHits, out RaycastHit physicsHit))
        {
            SetVirtualPhysicsRaycast(physicsHit);
        }

        virtualPointerData.pointerCurrentRaycast = virtualRaycast;
        GameObject nextHoveredObject = hasVirtualRaycast ? virtualRaycast.gameObject : null;

        if (nextHoveredObject != virtualHoveredObject)
        {
            if (virtualHoveredObject != null)
                ExecuteEvents.ExecuteHierarchy(virtualHoveredObject, virtualPointerData, ExecuteEvents.pointerExitHandler);

            virtualHoveredObject = nextHoveredObject;

            if (virtualHoveredObject != null)
                ExecuteEvents.ExecuteHierarchy(virtualHoveredObject, virtualPointerData, ExecuteEvents.pointerEnterHandler);
        }

        // Keep sending drag updates to the object that received pointer-down.
        // This gives draggable 3D objects pointer capture while the mouse is held.
        GameObject moveTarget = virtualPointerPressed && virtualPressedObject != null
            ? virtualPressedObject
            : virtualHoveredObject;

        if (moveTarget != null)
            ExecuteEvents.ExecuteHierarchy(moveTarget, virtualPointerData, ExecuteEvents.pointerMoveHandler);
    }

    void SetVirtualPhysicsRaycast(RaycastHit hit)
    {
        virtualRaycast = new RaycastResult
        {
            gameObject = hit.collider.gameObject,
            worldPosition = hit.point,
            screenPosition = virtualPointerData.position,
            distance = hit.distance,
            index = 0
        };
        hasVirtualRaycast = true;
    }

    bool TryGetClosestBrainHit(RaycastHit[] hits, out RaycastHit closestHit)
    {
        closestHit = default;
        bool foundHit = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<BrainOrbit>() == null)
                continue;

            if (!foundHit || hit.distance < closestHit.distance)
            {
                closestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    bool TryGetClosestPhysicsHit(RaycastHit[] hits, out RaycastHit closestHit)
    {
        closestHit = default;
        bool foundHit = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (!foundHit || hit.distance < closestHit.distance)
            {
                closestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    Vector2 ClampVirtualPointerViewport(Vector2 viewportPosition)
    {
        if (simScreen == null || simScreen.rect.width <= 0f || simScreen.rect.height <= 0f)
            return new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y));

        if (!mixed3DMouseMode || mixed3DPointer == null)
            return new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y));

        Rect screenRect = simScreen.rect;
        float pointerWidth = Mathf.Clamp01(mixed3DPointer.rect.width / screenRect.width);
        float pointerHeight = Mathf.Clamp01(mixed3DPointer.rect.height / screenRect.height);

        return new Vector2(
            Mathf.Clamp(viewportPosition.x, 0f, 1f - pointerWidth),
            Mathf.Clamp(viewportPosition.y, pointerHeight, 1f));
    }

    void PressVirtualPointer(PointerEventData sourceEvent)
    {
        if (simCamera == null || virtualPointerPressed || !hasVirtualRaycast)
            return;

        EnsureVirtualPointerData(sourceEvent);
        if (virtualPointerData == null)
            return;

        virtualPointerData.pointerCurrentRaycast = virtualRaycast;
        virtualPointerData.pointerPressRaycast = virtualRaycast;
        hasVirtualPosition = true;

        virtualPointerData.pressPosition = virtualPointerData.position;
        virtualPointerData.button = PointerEventData.InputButton.Left;
        virtualPointerData.clickCount = 1;
        virtualPointerData.clickTime = Time.unscaledTime;
        virtualPointerData.eligibleForClick = true;

        GameObject target = virtualRaycast.gameObject;
        virtualPressedObject = target;
        virtualPointerPressed = true;
        virtualPointerData.pointerPress = ExecuteEvents.ExecuteHierarchy(
            target, virtualPointerData, ExecuteEvents.pointerDownHandler);
        virtualPointerData.rawPointerPress = target;
    }

    void ReleaseVirtualPointer(bool sendClick)
    {
        if (!virtualPointerPressed || virtualPointerData == null)
            return;

        GameObject pressedObject = virtualPressedObject;
        bool releaseOverPressedObject = pressedObject != null &&
            (virtualHoveredObject == pressedObject ||
             (virtualHoveredObject != null &&
              (virtualHoveredObject.transform.IsChildOf(pressedObject.transform) ||
               pressedObject.transform.IsChildOf(virtualHoveredObject.transform))));

        virtualPointerData.pointerCurrentRaycast = virtualRaycast;
        if (pressedObject != null)
        {
            ExecuteEvents.ExecuteHierarchy(pressedObject, virtualPointerData, ExecuteEvents.pointerUpHandler);

            if (sendClick && virtualPointerData.eligibleForClick && releaseOverPressedObject)
                ExecuteEvents.ExecuteHierarchy(pressedObject, virtualPointerData, ExecuteEvents.pointerClickHandler);
        }

        virtualPointerData.pointerPress = null;
        virtualPointerData.rawPointerPress = null;
        virtualPointerData.eligibleForClick = false;
        virtualPressedObject = null;
        virtualPointerPressed = false;
    }

    bool TryGetVirtualBrainTarget(out BrainOrbit brain)
    {
        brain = null;
        if (!hasVirtualRaycast || virtualRaycast.gameObject == null)
            return false;

        brain = virtualRaycast.gameObject.GetComponentInParent<BrainOrbit>();
        return brain != null;
    }

    void ResetMixed3DBrain()
    {
        if (TryGetVirtualBrainTarget(out BrainOrbit brain))
        {
            brain.ResetRotation();
            return;
        }

        // Preserve the former right-click reset behavior even when the new
        // movable pointer is no longer directly over the single active brain.
        brain = FindFirstObjectByType<BrainOrbit>();
        if (brain != null)
            brain.ResetRotation();
    }

    void ClearVirtualHover()
    {
        if (virtualHoveredObject != null && virtualPointerData != null)
        {
            ExecuteEvents.ExecuteHierarchy(
                virtualHoveredObject, virtualPointerData, ExecuteEvents.pointerExitHandler);
        }

        virtualHoveredObject = null;
        hasVirtualRaycast = false;
        virtualRaycast = default;
    }

    void ResetVirtualPointerTracking()
    {
        hasVirtualPosition = false;
        hasLastPointerViewport = false;
        hasSmoothedPointerViewport = false;
    }

    bool TryGetSimulatedViewport(Vector3 screenWorldPosition, out Vector2 viewportPosition)
    {
        viewportPosition = Vector2.zero;
        if (simScreen == null || simCamera == null) return false;

        Rect screenRect = simScreen.rect;
        if (screenRect.width <= 0f || screenRect.height <= 0f) return false;

        Vector3 localPosition = simScreen.InverseTransformPoint(screenWorldPosition);
        viewportPosition = new Vector2(
            Mathf.Clamp01((localPosition.x - screenRect.xMin) / screenRect.width),
            Mathf.Clamp01((localPosition.y - screenRect.yMin) / screenRect.height));
        return true;
    }

    Vector2 ViewportToPixelPosition(Vector2 viewportPosition)
    {
        int width = simCamera.targetTexture != null ? simCamera.targetTexture.width : simCamera.pixelWidth;
        int height = simCamera.targetTexture != null ? simCamera.targetTexture.height : simCamera.pixelHeight;
        return new Vector2(viewportPosition.x * width, viewportPosition.y * height);
    }

    void EnsureVirtualPointerData(PointerEventData sourceEvent)
    {
        if (virtualPointerData == null)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            virtualPointerData = new PointerEventData(eventSystem);
        }

        if (sourceEvent != null)
        {
            virtualPointerData.pointerId = sourceEvent.pointerId;
            virtualPointerData.button = sourceEvent.button;
        }
    }

    void OnScreenPointerEnter(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        isPointerOverScreen = true;
        hasLastPointerViewport = false;
        hasSmoothedPointerViewport = false;

        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
        }
    }

    void OnScreenPointerMove(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        isPointerOverScreen = true;
        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
        }
    }

    void OnScreenPointerExit(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        isPointerOverScreen = false;
        ReleaseVirtualPointer(false);
        suppressNextPointerClick = false;
        ClearVirtualHover();
        ResetVirtualPointerTracking();
    }

    void OnScreenPointerClick(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        if (suppressNextPointerClick)
        {
            suppressNextPointerClick = false;
            return;
        }

        // Pointer click is normally generated after our explicit pointer-up. The
        // fallback keeps single-click behavior working if a relay sends click
        // without a preceding pointer-down.
        if (!virtualPointerPressed && TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
            PressVirtualPointer(eventData);
            ReleaseVirtualPointer(true);
        }
    }

    void OnScreenPointerDown(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        isPointerOverScreen = true;
        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
            PressVirtualPointer(eventData);
        }
    }

    void OnScreenPointerUp(PointerEventData eventData)
    {
        if (IsMixed3DMode())
            return;

        bool wasPressed = virtualPointerPressed;
        ReleaseVirtualPointer(true);
        suppressNextPointerClick = wasPressed;
    }

    bool TryGetPointerWorldPosition(PointerEventData eventData, out Vector3 worldPosition)
    {
        worldPosition = eventData.pointerCurrentRaycast.worldPosition;
        if (worldPosition != Vector3.zero) return true;

        worldPosition = eventData.pointerPressRaycast.worldPosition;
        return worldPosition != Vector3.zero;
    }

    float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    void OnDestroy()
    {
        if (mouseArrowSprite == null)
            return;

        Texture2D texture = mouseArrowSprite.texture;
        Destroy(mouseArrowSprite);
        if (texture != null)
            Destroy(texture);
    }

    sealed class ScreenPointerRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerMoveHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        SimulatedPlayer owner;
        public void Initialize(SimulatedPlayer simulatedPlayer) { owner = simulatedPlayer; }
        public void OnPointerEnter(PointerEventData eventData) { owner?.OnScreenPointerEnter(eventData); }
        public void OnPointerMove(PointerEventData eventData) { owner?.OnScreenPointerMove(eventData); }
        public void OnPointerExit(PointerEventData eventData) { owner?.OnScreenPointerExit(eventData); }
        public void OnPointerDown(PointerEventData eventData) { owner?.OnScreenPointerDown(eventData); }
        public void OnPointerUp(PointerEventData eventData) { owner?.OnScreenPointerUp(eventData); }
        public void OnPointerClick(PointerEventData eventData) { owner?.OnScreenPointerClick(eventData); }
    }
}
