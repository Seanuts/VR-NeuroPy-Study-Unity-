using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimulatedPlayer : MonoBehaviour
{
    [SerializeField] Camera simCamera;

    // Control variables
    const float mouseSensitivity = .25f;
    const float minPitch = -80f;
    const float maxPitch = 80f;

    float curYaw;
    float curPitch;

    void Start()
    {
        curYaw = transform.localEulerAngles.y;
        curPitch = transform.localEulerAngles.x;
    }

    void Update()
    {
        // TODO: Check if currently modifying brain
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        curYaw += mouseDelta.x * mouseSensitivity;
        curPitch -= mouseDelta.y * mouseSensitivity;
        curPitch = Mathf.Clamp(curPitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(curPitch, curYaw, 0f);
    }
}
