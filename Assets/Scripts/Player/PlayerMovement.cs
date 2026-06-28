using UnityEngine;
using Fusion;

public class PlayerMovement : NetworkBehaviour
{
    public float walkSpeed;

    PlayerInputHandler playerInput;
    CharacterController controller;
    NetworkCharacterController cc;

    void Awake()
    {
        playerInput = GetComponent<PlayerInputHandler>();
        controller = GetComponent<CharacterController>();
        cc = GetComponent<NetworkCharacterController>();
    }

    public override void FixedUpdateNetwork()
    {
        Move();
    }

    void Move()
    {
        if(GetInput(out NetworkInputData input))
        {
            Vector3 moveDir = new Vector3(input.moveInput.x, 0, input.moveInput.y);
            if(moveDir.sqrMagnitude > 1)
            {
                moveDir.Normalize();
            }
            moveDir *= walkSpeed * Runner.DeltaTime;
            cc.Move(moveDir);

            if (moveDir != Vector3.zero)
            {
                Quaternion moveRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, moveRot, 720f * Time.deltaTime);
            }
        }
    }
}
