using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class LeftJoystickBrainRotate : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty leftJoystickAction;

    [Header("Object To Rotate")]
    public Transform brainVisualRoot; // Assign BrainRotationPivot here

    [Header("Rotation Settings")]
    public float spinSpeed = 90f;
    public float tiltSpeed = 60f;
    public float deadZone = 0.15f;
    public bool invertTilt = false;

    private XRGrabInteractable grabInteractable;
    private bool grabbedByLeftHand = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);

        if (leftJoystickAction.action != null)
            leftJoystickAction.action.Enable();
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);

        if (leftJoystickAction.action != null)
            leftJoystickAction.action.Disable();
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        grabbedByLeftHand = IsLeftHand(args.interactorObject.transform);
        Debug.Log("Brain grabbed. Left hand? " + grabbedByLeftHand);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        grabbedByLeftHand = false;
    }

    private bool IsLeftHand(Transform interactorTransform)
    {
        Transform current = interactorTransform;

        while (current != null)
        {
            if (current.name.ToLower().Contains("left"))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void LateUpdate()
    {
        if (!grabbedByLeftHand || brainVisualRoot == null || leftJoystickAction.action == null)
            return;

        Vector2 joystick = leftJoystickAction.action.ReadValue<Vector2>();

        float horizontal = Mathf.Abs(joystick.x) > deadZone ? joystick.x : 0f;
        float vertical = Mathf.Abs(joystick.y) > deadZone ? joystick.y : 0f;

        if (horizontal == 0f && vertical == 0f)
            return;

        // Save the visual center before rotation
        Vector3 centerBefore = GetVisualCenter();

        // Left/right joystick = spin
        if (horizontal != 0f)
        {
            Quaternion yawRotation =
                Quaternion.AngleAxis(horizontal * spinSpeed * Time.deltaTime, Vector3.up);

            brainVisualRoot.rotation = yawRotation * brainVisualRoot.rotation;
        }

        // Up/down joystick = tilt
        if (vertical != 0f)
        {
            float tiltDirection = invertTilt ? 1f : -1f;

            Vector3 tiltAxis = Camera.main != null
                ? Camera.main.transform.right
                : Vector3.right;

            Quaternion pitchRotation =
                Quaternion.AngleAxis(vertical * tiltSpeed * Time.deltaTime * tiltDirection, tiltAxis);

            brainVisualRoot.rotation = pitchRotation * brainVisualRoot.rotation;
        }

        // Save the visual center after rotation
        Vector3 centerAfter = GetVisualCenter();

        // Move the visual root back so the brain does not orbit forward/backward
        brainVisualRoot.position += centerBefore - centerAfter;
    }

    private Vector3 GetVisualCenter()
    {
        Renderer[] renderers = brainVisualRoot.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return brainVisualRoot.position;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }
}