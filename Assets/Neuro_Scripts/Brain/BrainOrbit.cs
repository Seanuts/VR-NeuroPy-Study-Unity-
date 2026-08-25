using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.Rendering;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

public class BrainOrbit : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerMoveHandler
{
    [Header("Crosshair orbit")]
    [SerializeField] float crosshairOrbitSensitivity = 0.3f;
    [SerializeField] Color hoverHighlightColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] float hoverHighlightStrength = 0.35f;
    [SerializeField] bool createCrosshairCollider = true;

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
    bool isCrosshairOrbiting;
    bool isCrosshairHovered;
    int crosshairPointerId;
    Renderer[] hoverRenderers;
    MaterialPropertyBlock hoverPropertyBlock;
    SphereCollider crosshairCollider;
    GameObject selectionSphere;
    Collider selectionSphereCollider;
    Material selectionSphereMaterial;
    bool joystickActionEnabled;

    private void Awake()
    {
        initialRotation = transform.rotation;
        hoverRenderers = GetComponentsInChildren<Renderer>(true);
        hoverPropertyBlock = new MaterialPropertyBlock();

        if (createCrosshairCollider)
            CreateCrosshairCollider();
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
        isCrosshairOrbiting = false;
        isCrosshairHovered = false;
        crosshairPointerId = 0;
        SetHoverHighlight(false);

        SetJoystickActionEnabled(false);

        if (resetButtonAction != null)
        {
            resetButtonAction.action.performed -= OnResetPressed;
            resetButtonAction.action.Disable();
        }
    }

    private void Update()
    {
        bool isVirtual3D = IsVirtual3DMode();
        SetJoystickActionEnabled(isVirtual3D && rightJoystickAction != null &&
                                 rightJoystickAction.action != null);
        SetDebugSelectionSphereVisible(IsMixed3DMode());

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
        ResetRotation();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isCrosshairHovered = true;
        SetHoverHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isCrosshairHovered = false;
        if (!isCrosshairOrbiting)
            SetHoverHighlight(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        isCrosshairOrbiting = true;
        crosshairPointerId = eventData.pointerId;
        SetHoverHighlight(true);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isCrosshairOrbiting || eventData.pointerId != crosshairPointerId)
            return;

        Vector2 delta = eventData.delta;
        if (delta == Vector2.zero)
            return;

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
        float angle = localAxis.magnitude * crosshairOrbitSensitivity;
        Vector3 worldAxis = camTransform.TransformDirection(localAxis.normalized);
        transform.rotation = Quaternion.AngleAxis(angle, worldAxis) * transform.rotation;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == crosshairPointerId)
        {
            isCrosshairOrbiting = false;
            crosshairPointerId = 0;

            if (!isCrosshairHovered)
                SetHoverHighlight(false);
        }
    }

    public void ResetRotation()
    {
        transform.rotation = initialRotation;
    }

    void SetHoverHighlight(bool highlighted)
    {
        if (hoverRenderers == null)
            return;

        if (!highlighted)
        {
            foreach (Renderer renderer in hoverRenderers)
                if (renderer != null)
                    renderer.SetPropertyBlock(null);

            return;
        }

        foreach (Renderer renderer in hoverRenderers)
        {
            if (renderer == null)
                continue;

            Color baseColor = Color.white;
            Material material = renderer.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                    baseColor = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color"))
                    baseColor = material.GetColor("_Color");
            }

            Color highlightedColor = Color.Lerp(baseColor, hoverHighlightColor, hoverHighlightStrength);
            renderer.GetPropertyBlock(hoverPropertyBlock);

            if (material != null && material.HasProperty("_BaseColor"))
                hoverPropertyBlock.SetColor("_BaseColor", highlightedColor);
            if (material != null && material.HasProperty("_Color"))
                hoverPropertyBlock.SetColor("_Color", highlightedColor);

            renderer.SetPropertyBlock(hoverPropertyBlock);
        }
    }

    void CreateCrosshairCollider()
    {
        if (hoverRenderers == null || hoverRenderers.Length == 0)
            return;

        Bounds bounds = hoverRenderers[0].bounds;
        for (int i = 1; i < hoverRenderers.Length; i++)
            bounds.Encapsulate(hoverRenderers[i].bounds);

        crosshairCollider = gameObject.AddComponent<SphereCollider>();
        crosshairCollider.center = transform.InverseTransformPoint(bounds.center) +
            Vector3.up * selectionSphereVerticalOffset;

        Vector3 localExtents = transform.InverseTransformVector(bounds.extents);
        crosshairCollider.radius = localExtents.magnitude * selectionSphereSize;

        CreateSelectionSphereVisual();
    }

    void CreateSelectionSphereVisual()
    {
        if (!showSelectionSphere || crosshairCollider == null)
            return;

        selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        selectionSphere.name = "BrainSelectionDebugSphere";
        selectionSphere.transform.SetParent(transform, false);
        selectionSphere.transform.localPosition = crosshairCollider.center;
        selectionSphere.transform.localScale = Vector3.one * (crosshairCollider.radius * 2f);

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

    void SetDebugSelectionSphereVisible(bool visible)
    {
        if (crosshairCollider != null)
            crosshairCollider.enabled = visible;

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
