using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OrbitGrab : MonoBehaviour
{
    [SerializeField]
    private float rotationSpeed = 200f;

    private XRGrabInteractable grab;

    private Transform currentHand;

    private Vector3 previousHandPosition;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        currentHand = args.interactorObject.transform;

        previousHandPosition = currentHand.position;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        currentHand = null;
    }

    private void Update()
    {
        if (currentHand == null)
            return;

        Vector3 delta = currentHand.position - previousHandPosition;

        // Horizontal (world up)
        transform.Rotate(
            Vector3.up,
            delta.x * rotationSpeed,
            Space.World
        );

        // Vertical (camera right)
        transform.Rotate(
            Camera.main.transform.right,
            -delta.y * rotationSpeed,
            Space.World
        );

        previousHandPosition = currentHand.position;
    }
}