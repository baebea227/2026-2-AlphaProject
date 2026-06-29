using Fusion;
using UnityEngine;
using UnityEngine.Windows;

public class PlayerMovement : NetworkBehaviour
{
    private bool isLocalTesting = true;

    PlayerStatus status;
    PlayerInputHandler handler;
    CharacterController controller;
    NetworkCharacterController cc;

    void Awake()
    {
        status = GetComponent<PlayerStatus>();
        handler = GetComponent<PlayerInputHandler>();
        controller = GetComponent<CharacterController>();
    }

    public override void Spawned()
    {
        Debug.Log("This is Spawned");
        isLocalTesting = false;

        cc = GetComponent<NetworkCharacterController>();
        cc.acceleration = 100f;
        cc.braking = 100f;
    }

    void Start()
    {
        //isLocalTesting = true;
        Debug.Log("This is Start");
    }

    public override void FixedUpdateNetwork()
    {
        if (isLocalTesting)
        {
            return;
        }

        Move();
    }

    void FixedUpdate()
    {
        if (!isLocalTesting)
        {
            return;
        }

        MoveLocal();
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

    void MoveLocal()
    {
        NetworkInputData input = handler.GetInputData();
        Vector3 moveDir = new Vector3(input.moveInput.x, 0, input.moveInput.y);
        if (moveDir.sqrMagnitude > 1f)
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

        controller.Move(moveDir * moveSpeed * Time.deltaTime);
    }

    // 회전
    void Turn(Vector3 dir)
    {
        if(dir == Vector3.zero)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, 360f * (isLocalTesting ? Time.deltaTime : Runner.DeltaTime));
    }
}
