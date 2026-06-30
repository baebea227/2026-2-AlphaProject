using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction;
    InputAction sprintAction;
    [SerializeField] Camera cam;
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

    public NetworkInputData GetInputData()
    {
        return new NetworkInputData
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            isSprinting = sprintAction.IsPressed()
        };
    }
}
