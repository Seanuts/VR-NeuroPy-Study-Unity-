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

    [Header("Cursor")]
    [Tooltip("Arrow sprite shared by the Mixed2D and Mixed3D on-screen cursors. Assign in the Inspector.")]
    [SerializeField] Sprite cursorSprite;

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

    // Desktop/editor camera fallback used when no simulated mode is active.
    const float mouseSensitivity = .25f;
    const float minPitch = -80f;
    const float maxPitch = 80f;
    float curYaw;
    float curPitch;

    // Mixed3D on-screen pointer state.
    RectTransform mixed3DPointer;
    Vector2 mixed3DPointerViewport = new Vector2(.5f, .5f);
    bool mixed3DMouseMode;
    Transform mixed3DCameraTarget;

    // The simulated screen's canvas (drives the VR/Mixed3D virtual pointer).
    GraphicRaycaster simulatedCanvasRaycaster;
    Canvas simulatedCanvas;

    // External system-mouse canvas wiring for the flat Mixed2D slideshow.
    GraphicRaycaster externalMouseRaycaster;
    Canvas externalMouseCanvas;
    Canvas externalMouseCameraCanvas;
    Camera externalMouseOriginalCamera;
    RectTransform externalMouseBounds;
    RectTransform mouseCursor;
    Vector2 externalMouseBoundsPosition;
    bool hasExternalMouseBoundsPosition;
    bool externalMouseMode;
    float externalMouseSensitivity = 2f; // *2 since the previous value felt sluggish

    // VR pointer smoothing/tracking.
    Vector2 lastPointerViewport;
    Vector2 smoothedPointerViewport;
    bool hasLastPointerViewport;
    bool hasSmoothedPointerViewport;
    bool isPointerOverScreen;
    bool hasVirtualPosition;
    bool suppressNextPointerClick;

    // The two simulated pointers share one dispatch pipeline (see SimulatedPointer).
    readonly SimulatedPointer virtualPointer = new SimulatedPointer();
    readonly SimulatedPointer mousePointer = new SimulatedPointer();

    // EventSystem input modules disabled while the external mouse drives the UI.
    readonly List<BaseInputModule> disabledInputModules = new List<BaseInputModule>();
    readonly List<bool> disabledInputModuleStates = new List<bool>();

    void Awake()
    {
        if (simCamera == null)
            simCamera = GetComponent<Camera>();

        if (simScreen == null)
            simScreen = FindSiblingTransform("SimScreen") as RectTransform;

        if (cursorSprite == null)
            Debug.LogWarning("SimulatedPlayer: assign a Cursor Sprite in the Inspector for the on-screen pointers.");

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

        // GameScreen hosts the world-space canvas the virtual pointer draws onto.
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
        virtualPointer.Release(false);
        virtualPointer.ClearHover();

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
        // Fold the initial pitch into -180..180 so clamping behaves.
        curPitch = Mathf.Repeat(targetTransform.localEulerAngles.x + 180f, 360f) - 180f;
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

        // If the XR pointer is actively driving the view, do NOT also process desktop mouse delta.
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

    // ---------------------------------------------------------------------
    // Mode switching + cursor state
    // ---------------------------------------------------------------------

    void SetExternalMouseMode(bool enabled)
    {
        if (externalMouseMode == enabled)
            return;

        externalMouseMode = enabled;
        mousePointer.Release(false);
        mousePointer.ClearHover();
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

            // Turn off the scene's input modules so they don't fight our synthesized events.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                foreach (BaseInputModule inputModule in eventSystem.GetComponents<BaseInputModule>())
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

    // ---------------------------------------------------------------------
    // External system mouse (Mixed2D flat slideshow)
    // ---------------------------------------------------------------------

    void ProcessExternalMousePointer()
    {
        ConfigureExternalMouseCanvas();

        if (externalMouseRaycaster == null || externalMouseCanvas == null ||
            EventSystem.current == null || Mouse.current == null)
        {
            mousePointer.Release(false);
            mousePointer.ClearHover();
            SetMouseCursorVisible(false);
            return;
        }

        mousePointer.EnsureData(null);
        mousePointer.data.pointerId = -1;

        if (!hasExternalMouseBoundsPosition)
        {
            externalMouseBoundsPosition = ClampBoundsPosition(externalMouseBounds.rect.center);
            hasExternalMouseBoundsPosition = true;
        }

        // Move the cursor by mouse delta scaled into the interaction bounds.
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Vector2 boundsSize = externalMouseBounds.rect.size;
        externalMouseBoundsPosition += Vector2.Scale(mouseDelta / screenSize, boundsSize) * externalMouseSensitivity;
        externalMouseBoundsPosition = ClampBoundsPosition(externalMouseBoundsPosition);

        Vector2 mousePosition = BoundsLocalToCanvasPosition(externalMouseBoundsPosition);
        mousePointer.data.delta = mousePosition - mousePointer.data.position;
        mousePointer.data.position = mousePosition;
        mousePointer.data.scrollDelta = Mouse.current.scroll.ReadValue();
        mousePointer.data.button = PointerEventData.InputButton.Left;

        UpdateMouseCursor(externalMouseBoundsPosition);

        List<RaycastResult> results = new List<RaycastResult>();
        externalMouseRaycaster.Raycast(mousePointer.data, results);
        mousePointer.hasRaycast = results.Count > 0;
        mousePointer.raycast = results.Count > 0 ? results[0] : default;
        mousePointer.data.pointerCurrentRaycast = mousePointer.raycast;

        mousePointer.UpdateHover(results.Count > 0 ? results[0].gameObject : null);
        mousePointer.Move(mousePointer.hovered);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            mousePointer.Press(mousePointer.hovered);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            mousePointer.Release(true);
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

            // World-space canvases need a camera to raycast; remember the original to restore later.
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

    // Clamp a bounds-local position so the whole cursor arrow stays on screen.
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
    {
        if (mouseCursor == null)
        {
            mouseCursor = new GameObject("DesktopMouseCursor", typeof(RectTransform), typeof(Image))
                .GetComponent<RectTransform>();
            mouseCursor.anchorMin = Vector2.zero;
            mouseCursor.anchorMax = Vector2.zero;
            mouseCursor.pivot = new Vector2(0f, 1f);
            mouseCursor.sizeDelta = new Vector2(18f, 18f);

            Image image = mouseCursor.GetComponent<Image>();
            image.sprite = cursorSprite;
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

    // ---------------------------------------------------------------------
    // Mixed3D pointer + camera
    // ---------------------------------------------------------------------

    void CreateMixed3DPointer()
    {
        if (mixed3DPointer != null || simScreen == null)
            return;

        mixed3DPointer = new GameObject("Mixed3DMouseCursor", typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        mixed3DPointer.SetParent(simScreen, false);
        mixed3DPointer.anchorMin = new Vector2(.5f, .5f);
        mixed3DPointer.anchorMax = new Vector2(.5f, .5f);
        mixed3DPointer.pivot = new Vector2(0f, 1f);
        mixed3DPointer.sizeDelta = Vector2.one * mixed3DPointerSize;
        mixed3DPointer.SetAsLastSibling();

        Image image = mixed3DPointer.GetComponent<Image>();
        image.sprite = cursorSprite;
        image.color = mixed3DPointerColor;
        image.raycastTarget = false;

        Outline outline = mixed3DPointer.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .95f);
        outline.effectDistance = new Vector2(mixed3DPointerOutlineThickness, -mixed3DPointerOutlineThickness);
        outline.useGraphicAlpha = true;
    }

    void SetMixed3DMouseMode(bool enabled)
    {
        if (mixed3DMouseMode == enabled)
            return;

        mixed3DMouseMode = enabled;
        virtualPointer.Release(false);
        virtualPointer.ClearHover();
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
        mixed3DPointerViewport += Vector2.Scale(mouseDelta / screenSize, Vector2.one) * mixed3DMouseSensitivity;
        mixed3DPointerViewport = ClampVirtualPointerViewport(mixed3DPointerViewport);

        UpdateVirtualPointerAtViewport(mixed3DPointerViewport, null, null, false);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            PressVirtualPointer(null);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            virtualPointer.Release(true);

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
                cameraTransform.rotation = Quaternion.LookRotation(forward.normalized, mixed3DCameraTarget.up);
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

    // ---------------------------------------------------------------------
    // Virtual pointer (VR ray + Mixed3D mouse share this pipeline)
    // ---------------------------------------------------------------------

    void UpdateVirtualPointer(Vector3 screenWorldPosition, PointerEventData sourceEvent)
    {
        if (simCamera == null)
            return;

        if (TryGetSimulatedViewport(screenWorldPosition, out Vector2 pointerViewport))
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

        if (!virtualPointer.EnsureData(sourceEvent))
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

        Vector2 texSize = new Vector2(
            simCamera.targetTexture != null ? simCamera.targetTexture.width : simCamera.pixelWidth,
            simCamera.targetTexture != null ? simCamera.targetTexture.height : simCamera.pixelHeight);

        // --- 1. Pointer movement / drag delta ---
        Vector2 pointerDelta = Vector2.zero;
        if (hasLastPointerViewport)
        {
            pointerDelta = smoothedPointerViewport - lastPointerViewport;

            if (smoothPointer)
            {
                Vector2 deadzonePixelDelta = pointerDelta * texSize;
                if (deadzonePixelDelta.sqrMagnitude < pointerDeadzonePixels * pointerDeadzonePixels)
                    pointerDelta = Vector2.zero;

                // Ignore implausibly large controller-tracking jumps.
                if (pointerDelta.sqrMagnitude >= 0.01f)
                    pointerDelta = Vector2.zero;
            }
        }
        lastPointerViewport = smoothedPointerViewport;
        hasLastPointerViewport = true;

        // --- 2. Pointer raycast interaction ---
        Vector2 interactionViewport = smoothedPointerViewport;
        virtualPointer.data.position = ViewportToPixelPosition(interactionViewport);
        Vector2 pointerPixelDelta = forcedPixelDelta ?? pointerDelta * texSize;
        virtualPointer.data.delta = hasVirtualPosition ? pointerPixelDelta : Vector2.zero;
        hasVirtualPosition = true;

        if (mixed3DMouseMode)
        {
            mixed3DPointerViewport = smoothedPointerViewport;
            UpdateMixed3DPointerVisual();
        }

        List<RaycastResult> results = new List<RaycastResult>();
        if (simulatedCanvasRaycaster != null)
            simulatedCanvasRaycaster.Raycast(virtualPointer.data, results);
        virtualPointer.hasRaycast = results.Count > 0;
        virtualPointer.raycast = virtualPointer.hasRaycast ? results[0] : default;

        Ray ray = simCamera.ViewportPointToRay(new Vector3(interactionViewport.x, interactionViewport.y, 0f));
        RaycastHit[] physicsHits = Physics.RaycastAll(ray, simCamera.farClipPlane);

        // Prefer a brain hit over a canvas hit so the debug sphere stays a reliable
        // target when UI graphics are layered over the simulated view.
        if (TryGetClosestBrainHit(physicsHits, out RaycastHit brainHit))
            SetVirtualPhysicsRaycast(brainHit);
        else if (!virtualPointer.hasRaycast && TryGetClosestPhysicsHit(physicsHits, out RaycastHit physicsHit))
            SetVirtualPhysicsRaycast(physicsHit);

        virtualPointer.data.pointerCurrentRaycast = virtualPointer.raycast;
        virtualPointer.UpdateHover(virtualPointer.hasRaycast ? virtualPointer.raycast.gameObject : null);

        // While held, keep sending moves to the pressed object so draggable 3D
        // objects get pointer capture instead of losing it to whatever is hovered.
        GameObject moveTarget = virtualPointer.isPressed && virtualPointer.pressed != null
            ? virtualPointer.pressed
            : virtualPointer.hovered;
        virtualPointer.Move(moveTarget);
    }

    void SetVirtualPhysicsRaycast(RaycastHit hit)
    {
        virtualPointer.raycast = new RaycastResult
        {
            gameObject = hit.collider.gameObject,
            worldPosition = hit.point,
            screenPosition = virtualPointer.data.position,
            distance = hit.distance,
            index = 0
        };
        virtualPointer.hasRaycast = true;
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
        // Without a sized screen or a Mixed3D pointer, just clamp to 0..1.
        if (simScreen == null || simScreen.rect.width <= 0f || simScreen.rect.height <= 0f ||
            !mixed3DMouseMode || mixed3DPointer == null)
        {
            return new Vector2(Mathf.Clamp01(viewportPosition.x), Mathf.Clamp01(viewportPosition.y));
        }

        // Keep the whole Mixed3D arrow inside the screen (pivot is top-left).
        Rect screenRect = simScreen.rect;
        float pointerWidth = Mathf.Clamp01(mixed3DPointer.rect.width / screenRect.width);
        float pointerHeight = Mathf.Clamp01(mixed3DPointer.rect.height / screenRect.height);

        return new Vector2(
            Mathf.Clamp(viewportPosition.x, 0f, 1f - pointerWidth),
            Mathf.Clamp(viewportPosition.y, pointerHeight, 1f));
    }

    void PressVirtualPointer(PointerEventData sourceEvent)
    {
        if (simCamera == null || virtualPointer.isPressed || !virtualPointer.hasRaycast)
            return;

        if (!virtualPointer.EnsureData(sourceEvent))
            return;

        hasVirtualPosition = true;
        virtualPointer.Press(virtualPointer.raycast.gameObject);
    }

    bool TryGetVirtualBrainTarget(out BrainOrbit brain)
    {
        brain = null;
        if (!virtualPointer.hasRaycast || virtualPointer.raycast.gameObject == null)
            return false;

        brain = virtualPointer.raycast.gameObject.GetComponentInParent<BrainOrbit>();
        return brain != null;
    }

    void ResetMixed3DBrain()
    {
        if (TryGetVirtualBrainTarget(out BrainOrbit brain))
        {
            brain.ResetRotation();
            return;
        }

        // Preserve the former right-click reset even when the movable pointer is
        // no longer directly over the single active brain.
        brain = FindFirstObjectByType<BrainOrbit>();
        if (brain != null)
            brain.ResetRotation();
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

    // ---------------------------------------------------------------------
    // Screen pointer relay callbacks (VR ray hitting the SimScreen)
    // ---------------------------------------------------------------------

    void OnScreenPointerEnter(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        isPointerOverScreen = true;
        hasLastPointerViewport = false;
        hasSmoothedPointerViewport = false;

        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
            UpdateVirtualPointer(worldPosition, eventData);
    }

    void OnScreenPointerMove(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        isPointerOverScreen = true;
        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
            UpdateVirtualPointer(worldPosition, eventData);
    }

    void OnScreenPointerExit(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        isPointerOverScreen = false;
        virtualPointer.Release(false);
        suppressNextPointerClick = false;
        virtualPointer.ClearHover();
        ResetVirtualPointerTracking();
    }

    void OnScreenPointerClick(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        if (suppressNextPointerClick)
        {
            suppressNextPointerClick = false;
            return;
        }

        // Click normally follows our explicit pointer-up. This fallback keeps
        // single clicks working if a relay sends click without a preceding down.
        if (!virtualPointer.isPressed && TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
            PressVirtualPointer(eventData);
            virtualPointer.Release(true);
        }
    }

    void OnScreenPointerDown(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        isPointerOverScreen = true;
        if (TryGetPointerWorldPosition(eventData, out Vector3 worldPosition))
        {
            UpdateVirtualPointer(worldPosition, eventData);
            PressVirtualPointer(eventData);
        }
    }

    void OnScreenPointerUp(PointerEventData eventData)
    {
        if (IsMixed3DMode()) return;

        bool wasPressed = virtualPointer.isPressed;
        virtualPointer.Release(true);
        suppressNextPointerClick = wasPressed;
    }

    bool TryGetPointerWorldPosition(PointerEventData eventData, out Vector3 worldPosition)
    {
        worldPosition = eventData.pointerCurrentRaycast.worldPosition;
        if (worldPosition != Vector3.zero) return true;

        worldPosition = eventData.pointerPressRaycast.worldPosition;
        return worldPosition != Vector3.zero;
    }

    // ---------------------------------------------------------------------
    // Misc helpers
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Shared pointer dispatch
    // ---------------------------------------------------------------------

    // Bundles the PointerEventData plumbing shared by the two simulated pointers:
    // the Mixed2D system mouse and the virtual pointer (VR ray + Mixed3D mouse).
    // Positioning and raycasting differ per input source and stay with the caller;
    // this owns only hover enter/exit, press, release, and click dispatch.
    sealed class SimulatedPointer
    {
        public PointerEventData data;
        public RaycastResult raycast;
        public bool hasRaycast;
        public GameObject hovered;
        public GameObject pressed;
        public bool isPressed;

        // Create the PointerEventData lazily (returns false if there's no EventSystem yet).
        public bool EnsureData(PointerEventData source)
        {
            if (data == null)
            {
                if (EventSystem.current == null)
                    return false;
                data = new PointerEventData(EventSystem.current);
            }

            if (source != null)
            {
                data.pointerId = source.pointerId;
                data.button = source.button;
            }
            return true;
        }

        // Fire exit on the old target and enter on the new one when hover changes.
        public void UpdateHover(GameObject next)
        {
            if (next == hovered)
                return;

            if (hovered != null)
                ExecuteEvents.ExecuteHierarchy(hovered, data, ExecuteEvents.pointerExitHandler);

            hovered = next;
            data.pointerEnter = hovered;

            if (hovered != null)
                ExecuteEvents.ExecuteHierarchy(hovered, data, ExecuteEvents.pointerEnterHandler);
        }

        public void Move(GameObject target)
        {
            if (target != null)
                ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerMoveHandler);
        }

        public void Press(GameObject target)
        {
            if (isPressed || target == null || data == null)
                return;

            data.pointerCurrentRaycast = raycast;
            data.pointerPressRaycast = raycast;
            data.pressPosition = data.position;
            data.clickCount = 1;
            data.clickTime = Time.unscaledTime;
            data.eligibleForClick = true;
            data.button = PointerEventData.InputButton.Left;

            pressed = target;
            isPressed = true;
            data.pointerPress = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            data.rawPointerPress = target;
        }

        public void Release(bool sendClick)
        {
            if (!isPressed || data == null)
                return;

            // A click only counts if the release lands on the pressed object, or a
            // child of it either direction, matching Unity's own click rules.
            bool overPressed = pressed != null &&
                (hovered == pressed ||
                 (hovered != null &&
                  (hovered.transform.IsChildOf(pressed.transform) ||
                   pressed.transform.IsChildOf(hovered.transform))));

            data.pointerCurrentRaycast = raycast;
            if (pressed != null)
            {
                ExecuteEvents.ExecuteHierarchy(pressed, data, ExecuteEvents.pointerUpHandler);

                if (sendClick && data.eligibleForClick && overPressed)
                    ExecuteEvents.ExecuteHierarchy(pressed, data, ExecuteEvents.pointerClickHandler);
            }

            data.pointerPress = null;
            data.rawPointerPress = null;
            data.eligibleForClick = false;
            pressed = null;
            isPressed = false;
        }

        public void ClearHover()
        {
            if (hovered != null && data != null)
                ExecuteEvents.ExecuteHierarchy(hovered, data, ExecuteEvents.pointerExitHandler);

            if (data != null)
                data.pointerEnter = null;

            hovered = null;
            raycast = default;
            hasRaycast = false;
        }
    }

    // Relays SimScreen pointer events (from the XR ray) back to the owner.
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
