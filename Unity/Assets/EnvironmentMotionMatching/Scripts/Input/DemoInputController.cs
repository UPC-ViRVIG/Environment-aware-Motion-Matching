using MotionMatching;
using UnityEngine;
using UnityEngine.InputSystem;

public class DemoInputController : MonoBehaviour
{
    private bool IsWalking = false;
    private float MaxSpeed = 2.0f;

    private CrowdCharacterController CrowdCharacterController;

    private void Awake()
    {
        CrowdCharacterController = GetComponent<CrowdCharacterController>();
        if (CrowdCharacterController != null)
        {
            MaxSpeed = CrowdCharacterController.MaxSpeed;
        }
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            IsWalking = !IsWalking;

            if (IsWalking)
            {
                CrowdCharacterController.MaxSpeed = MaxSpeed / 2.0f;
            }
            else
            {
                CrowdCharacterController.MaxSpeed = MaxSpeed;
            }
        }
    }
}
