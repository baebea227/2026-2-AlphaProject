using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction;
    InputAction sprintAction;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            playerInput.enabled = true;

            if (Runner != null && Object != null)
                Runner.SetPlayerObject(Object.InputAuthority, Object);

            return;
        }

        playerInput.enabled = false;
    }

    public NetworkInputData GetInputData()
    {
        return new NetworkInputData
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            isSprinting = sprintAction.IsPressed()
        };
    }
}
