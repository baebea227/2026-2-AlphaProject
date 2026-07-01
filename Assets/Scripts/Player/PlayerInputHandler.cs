using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{


    PlayerInput playerInput;
    InputAction moveAction;
    InputAction sprintAction;
    InputAction interactAction;

    bool interActionPressed;

    [SerializeField] Camera cam;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
        interactAction = playerInput.actions["Interact"];
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            playerInput.enabled = true;

            if(cam != null)
            {
                cam.gameObject.SetActive(true);
            }

            return;
        }

        playerInput.enabled = false;

        Camera remoteCam = GetComponentInChildren<Camera>();
        if (remoteCam != null)
        {
            remoteCam.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (interactAction.WasPressedThisFrame())
        {
            interActionPressed = true;
        }
    }

    public NetworkInputData GetInputData()
    {
        NetworkInputData data = new NetworkInputData
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            isSprinting = sprintAction.IsPressed(),
            isInteract = interActionPressed
        };

        interActionPressed = false;

        return data;
    }
}
