using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

public class BrainOrbit : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerMoveHandler
{
    [Header("Mouse orbit")]
    [FormerlySerializedAs("crosshairOrbitSensitivity")]
    [SerializeField, Min(0f)] float mouseOrbitSensitivity = 0.3f;
    [SerializeField, Range(0f, 1f)] float momentumStrength = 0.1f;
    [SerializeField, Min(0f)] float momentumDamping = 10f;
    [FormerlySerializedAs("createCrosshairCollider")]
    [SerializeField] bool createMouseInteractionCollider = true;

    [Header("Debug selection sphere")]
    // These remain source-controlled so changing the values below is not
    // overridden by Unity's previously serialized component values.
    [Min(0.01f)] float selectionSphereSize = 0.6f;
    float selectionSphereVerticalOffset = 0f;
    bool showSelectionSphere = false;
    [SerializeField] Color selectionSphereColor = new Color(1f, 0f, 0f, 0.25f);

    [Header("Legacy joystick orbit")]
    [SerializeField] float rotationSpeed = 200f;
    [SerializeField] private InputActionReference rightJoystickAction;
    [SerializeField, Min(0f)] float joystickDeadzone = 0.15f;
    [SerializeField] private InputActionReference resetButtonAction;

    Quaternion initialRotation;
    bool isMouseOrbiting;
    int mousePointerId;
    Vector3 momentumDegreesPerSecond;
    Renderer[] brainRenderers;
    SphereCollider mouseInteractionCollider;
    GameObject selectionSphere;
    Collider selectionSphereCollider;
    Material selectionSphereMaterial;
    bool joystickActionEnabled;
    bool wasRightControllerAPressed;

    private void Awake()
    {
        initialRotation = transform.rotation;
        brainRenderers = GetComponentsInChildren<Renderer>(true);

        if (createMouseInteractionCollider)
            CreateMouseInteractionCollider();
    }
    private void OnEnable()
    {
        if (resetButtonAction != null)
        {
            resetButtonAction.action.Enable();
            resetButtonAction.action.performed += OnResetPressed;
        }
    }

    private void OnDisable()
    {
        isMouseOrbiting = false;
        mousePointerId = 0;
        momentumDegreesPerSecond = Vector3.zero;

        SetJoystickActionEnabled(false);
        wasRightControllerAPressed = false;

        if (resetButtonAction != null)
        {
            resetButtonAction.action.performed -= OnResetPressed;
            resetButtonAction.action.Disable();
        }
    }

    private void Update()
    {
        bool isVirtual3D = IsVirtual3DMode();
        bool isMixed3D = IsMixed3DMode();
        SetJoystickActionEnabled(isVirtual3D && rightJoystickAction != null &&
                                 rightJoystickAction.action != null);
        SetDebugSelectionSphereVisible(isMixed3D);
        UpdateVirtual3DResetInput(isVirtual3D);

        if (isMixed3D && !isMouseOrbiting)
            ApplyMomentum();

        if (!isVirtual3D)
            return;

        Vector2 joystickInput = Vector2.zero;
        if (rightJoystickAction != null && rightJoystickAction.action != null)
            joystickInput = rightJoystickAction.action.ReadValue<Vector2>();

        if (joystickInput.sqrMagnitude <= joystickDeadzone * joystickDeadzone)
            TryReadRightControllerStick(out joystickInput);

        if (joystickInput.sqrMagnitude > joystickDeadzone * joystickDeadzone)
        {
            Transform camTransform = Camera.main != null ? Camera.main.transform : transform;

            // X-axis right-stick input controls horizontal rotation (around global Up)
            transform.Rotate(Vector3.up, -joystickInput.x * rotationSpeed * Time.deltaTime, Space.World);

            // Y-axis right-stick input controls vertical rotation (around camera's Right)
            transform.Rotate(camTransform.right, joystickInput.y * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnResetPressed(InputAction.CallbackContext context)
    {
        if (IsVirtual3DMode())
            ResetRotation();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        isMouseOrbiting = true;
        mousePointerId = eventData.pointerId;
        momentumDegreesPerSecond = Vector3.zero;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isMouseOrbiting || eventData.pointerId != mousePointerId)
            return;

        Vector2 delta = eventData.delta;
        if (delta == Vector2.zero)
        {
            momentumDegreesPerSecond = Vector3.zero;
            return;
        }

        Camera simulatedCamera = SimulatedPlayer.ActiveSimulatedCamera;
        Transform camTransform = simulatedCamera != null
            ? simulatedCamera.transform
            : (Camera.main != null ? Camera.main.transform : null);

        if (camTransform == null)
            return;

        // Treat the pointer as a virtual trackball: the drag direction defines
        // the screen-space rotation axis, so flicking in different directions
        // produces a different globe-like rotation.
        Vector3 localAxis = new Vector3(delta.y, -delta.x, 0f);
        float angle = localAxis.magnitude * mouseOrbitSensitivity;
        Vector3 worldAxis = camTransform.TransformDirection(localAxis.normalized);
        transform.rotation = Quaternion.AngleAxis(angle, worldAxis) * transform.rotation;

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        momentumDegreesPerSecond = worldAxis * (angle / deltaTime) * momentumStrength;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == mousePointerId)
        {
            isMouseOrbiting = false;
            mousePointerId = 0;
        }
    }

    public void ResetRotation()
    {
        momentumDegreesPerSecond = Vector3.zero;
        transform.rotation = initialRotation;
    }

    void ApplyMomentum()
    {
        float speed = momentumDegreesPerSecond.magnitude;
        if (speed < 0.05f)
        {
            momentumDegreesPerSecond = Vector3.zero;
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        transform.rotation = Quaternion.AngleAxis(
            speed * deltaTime, momentumDegreesPerSecond / speed) * transform.rotation;
        momentumDegreesPerSecond *= Mathf.Exp(-momentumDamping * deltaTime);
    }

    void CreateMouseInteractionCollider()
    {
        if (brainRenderers == null || brainRenderers.Length == 0)
            return;

        Bounds bounds = brainRenderers[0].bounds;
        for (int i = 1; i < brainRenderers.Length; i++)
            bounds.Encapsulate(brainRenderers[i].bounds);

        mouseInteractionCollider = gameObject.AddComponent<SphereCollider>();
        mouseInteractionCollider.center = transform.InverseTransformPoint(bounds.center) +
            Vector3.up * selectionSphereVerticalOffset;

        Vector3 localExtents = transform.InverseTransformVector(bounds.extents);
        mouseInteractionCollider.radius = localExtents.magnitude * selectionSphereSize;

        CreateSelectionSphereVisual();
    }

    void CreateSelectionSphereVisual()
    {
        if (!showSelectionSphere || mouseInteractionCollider == null)
            return;

        selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        selectionSphere.name = "BrainSelectionDebugSphere";
        selectionSphere.transform.SetParent(transform, false);
        selectionSphere.transform.localPosition = mouseInteractionCollider.center;
        selectionSphere.transform.localScale = Vector3.one * (mouseInteractionCollider.radius * 2f);

        Collider visualCollider = selectionSphere.GetComponent<Collider>();
        if (visualCollider != null)
        {
            visualCollider.isTrigger = false;
            selectionSphereCollider = visualCollider;
        }

        MeshRenderer renderer = selectionSphere.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (renderer == null || shader == null)
            return;

        selectionSphereMaterial = new Material(shader);
        selectionSphereMaterial.color = selectionSphereColor;
        ConfigureSelectionSphereTransparency(selectionSphereMaterial);
        renderer.sharedMaterial = selectionSphereMaterial;
    }

    void SetJoystickActionEnabled(bool shouldBeEnabled)
    {
        if (rightJoystickAction == null || rightJoystickAction.action == null ||
            joystickActionEnabled == shouldBeEnabled)
            return;

        if (shouldBeEnabled)
            rightJoystickAction.action.Enable();
        else
            rightJoystickAction.action.Disable();

        joystickActionEnabled = shouldBeEnabled;
    }

    bool TryReadRightControllerStick(out Vector2 stickInput)
    {
        stickInput = Vector2.zero;
        XRInputDevice rightController = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!rightController.isValid)
            return false;

        bool hasPrimaryAxis = rightController.TryGetFeatureValue(
            XRCommonUsages.primary2DAxis, out Vector2 primaryAxis);
        if (hasPrimaryAxis)
            stickInput = primaryAxis;

        Vector2 secondaryAxis;
        bool hasSecondaryAxis = rightController.TryGetFeatureValue(
            XRCommonUsages.secondary2DAxis, out secondaryAxis);
        if (stickInput.sqrMagnitude <= joystickDeadzone * joystickDeadzone &&
            hasSecondaryAxis && secondaryAxis.sqrMagnitude > stickInput.sqrMagnitude)
        {
            stickInput = secondaryAxis;
        }

        return hasPrimaryAxis || stickInput != Vector2.zero;
    }

    void UpdateVirtual3DResetInput(bool isVirtual3D)
    {
        XRInputDevice rightController = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool isAPressed = rightController.isValid &&
                          rightController.TryGetFeatureValue(
                              XRCommonUsages.primaryButton, out bool primaryButtonPressed) &&
                          primaryButtonPressed;

        if (isVirtual3D && isAPressed && !wasRightControllerAPressed)
            ResetRotation();

        wasRightControllerAPressed = isAPressed;
    }

    void SetDebugSelectionSphereVisible(bool visible)
    {
        if (mouseInteractionCollider != null)
            mouseInteractionCollider.enabled = visible;

        if (selectionSphereCollider != null)
            selectionSphereCollider.enabled = visible;

        if (selectionSphere != null)
            selectionSphere.SetActive(visible && showSelectionSphere);
    }

    bool IsVirtual3DMode()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.CurrentMode() == GameModes.VIRTUAL_3D;
    }

    bool IsMixed3DMode()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.CurrentMode() == GameModes.MIXED_3D;
    }

    void ConfigureSelectionSphereTransparency(Material material)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZTest", (int)CompareFunction.Always);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    void OnDestroy()
    {
        if (selectionSphereMaterial != null)
            Destroy(selectionSphereMaterial);
    }
}
