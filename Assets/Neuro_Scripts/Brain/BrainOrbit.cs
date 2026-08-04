using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BrainOrbit : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 200f;
    [SerializeField] private InputActionReference leftJoystickAction;
    [SerializeField] private InputActionReference resetButtonAction;

    Quaternion initialRotation;

    private void Awake()
    {
        initialRotation = transform.rotation;
    }
    private void OnEnable()
    {
        // Enable actions and bind the reset button event
        if (leftJoystickAction != null)
            leftJoystickAction.action.Enable();

        if (resetButtonAction != null)
        {
            resetButtonAction.action.Enable();
            resetButtonAction.action.performed += OnResetPressed;
        }
    }

    private void OnDisable()
    {
        // Clean up events and disable actions to prevent memory leaks
        if (leftJoystickAction != null)
            leftJoystickAction.action.Disable();

        if (resetButtonAction != null)
        {
            resetButtonAction.action.performed -= OnResetPressed;
            resetButtonAction.action.Disable();
        }
    }

    private void Update()
    {
        if (leftJoystickAction == null) return;

        // Read the 2D vector from the left joystick
        Vector2 joystickInput = leftJoystickAction.action.ReadValue<Vector2>();

        if (joystickInput != Vector2.zero)
        {
            Transform camTransform = Camera.main.transform;

            // X-axis joystick input controls horizontal rotation (around global Up)
            transform.Rotate(Vector3.up, -joystickInput.x * rotationSpeed * Time.deltaTime, Space.World);

            // Y-axis joystick input controls vertical rotation (around camera's Right)
            transform.Rotate(camTransform.right, joystickInput.y * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnResetPressed(InputAction.CallbackContext context)
    {
        ResetRotation();
    }

    public void ResetRotation()
    {
        transform.rotation = initialRotation;
    }
}