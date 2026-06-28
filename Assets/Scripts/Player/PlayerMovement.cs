using UnityEngine;
using Fusion;

public class PlayerMovement : NetworkBehaviour
{
    PlayerStatus status;
    CharacterController controller;
    NetworkCharacterController cc;

    void Awake()
    {
        status = GetComponent<PlayerStatus>();
        controller = GetComponent<CharacterController>();
        cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        cc.acceleration = 100f;
        cc.braking = 100f;
    }

    public override void FixedUpdateNetwork()
    {
        Move();
    }

    // 이동
    void Move()
    {
        if(GetInput(out NetworkInputData input))
        {
            Vector3 moveDir = new Vector3(input.moveInput.x, 0, input.moveInput.y);
            if(moveDir.sqrMagnitude > 1f)
            {
                moveDir.Normalize();
            }
            Turn(moveDir);

            float moveSpeed = 0f;
            if (input.isSprinting && status.Stamina > 0)
            {
                moveSpeed = status.SprintSpeed;
            }
            else if (input.moveInput.magnitude > 0f)
            {
                moveSpeed = status.WalkSpeed;
            }
            cc.maxSpeed = moveSpeed;

            cc.Move(moveDir * Runner.DeltaTime);
        }
    }

    // 회전
    void Turn(Vector3 dir)
    {
        if(dir == Vector3.zero)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, 360f * Runner.DeltaTime);
    }
}
