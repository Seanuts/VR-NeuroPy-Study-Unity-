using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OrbitGrab : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 200f;
    private XRGrabInteractable grab;
    private Transform currentHand;
    private Transform xrOriginTransform; // Tracks the player's playspace
    private Vector3 previousLocalHandPosition; // Tracks local position instead of world

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);

        // Find the XR Origin (or Camera Rig) in your scene automatically
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            xrOriginTransform = origin.transform;
        }
        else
        {
            // Fallback if XR Origin component isn't found
            Debug.LogWarning("XROrigin not found! Falling back to Main Camera's parent.");
            xrOriginTransform = Camera.main.transform.parent;
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        currentHand = args.interactorObject.transform;

        // Store the hand's initial position relative to the player's space
        if (xrOriginTransform != null)
            previousLocalHandPosition = xrOriginTransform.InverseTransformPoint(currentHand.position);
        else
            previousLocalHandPosition = currentHand.position;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        currentHand = null;
    }

    private void Update()
    {
        if (currentHand == null)
            return;

        Vector3 worldDelta;

        if (xrOriginTransform != null)
        {
            // 1. Calculate delta using the hand's position relative to the moving player
            Vector3 currentLocalHandPosition = xrOriginTransform.InverseTransformPoint(currentHand.position);
            Vector3 localHandDelta = currentLocalHandPosition - previousLocalHandPosition;

            // 2. Convert that safe delta back into a world direction for the rotations below
            worldDelta = xrOriginTransform.TransformDirection(localHandDelta);
            previousLocalHandPosition = currentLocalHandPosition;
        }
        else
        {
            // Fallback behavior if no player rig is found
            worldDelta = currentHand.position - previousLocalHandPosition;
            previousLocalHandPosition = currentHand.position;
        }

        // 3. Convert the safe movement into Camera space to keep your multi-axis fix working
        Transform camTransform = Camera.main.transform;
        Vector3 cameraLocalDelta = camTransform.InverseTransformDirection(worldDelta);

        // 4. Apply rotations safely
        transform.Rotate(
            Vector3.up,
            -cameraLocalDelta.x * rotationSpeed,
            Space.World
        );

        transform.Rotate(
            camTransform.right,
            cameraLocalDelta.y * rotationSpeed,
            Space.World
        );
    }
}