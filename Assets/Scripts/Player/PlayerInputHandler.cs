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

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new NetworkInputData
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            isSprinting = sprintAction.IsPressed()
        };

        input.Set(data);
    }
}
