using Fusion;
using UnityEngine;

public class PlayerStatus : NetworkBehaviour
{
    [Header("Basic Status")]
    public int initialHp;
    public float initialStamina;
    public float staminaConsumeRate;
    public float staminaRegenRate;
    int localHp;
    float localStamina;
    [Networked] public int NetHp { get; set; }
    [Networked] public float NetStamina { get; set; }
    public int Hp
    {
        get => isSpawned ? NetHp : localHp;
        set { if (isSpawned) NetHp = value; else localHp = value; }
    }
    public float Stamina
    {
        get => isSpawned ? NetStamina : localStamina;
        set { if (isSpawned) NetStamina = value; else localStamina = value; }
    }
    [Space(15)]

    [Header("Movement")]
    public float initialWalkSpeed;
    public float initialSprintSpeed;
    float localWalkSpeed;
    float localSprintSpeed;
    [Networked] public float NetWalkSpeed { get; set; }
    [Networked] public float NetSprintSpeed { get; set; }
    public float WalkSpeed
    {
        get => isSpawned ? NetWalkSpeed : localWalkSpeed;
        set { if (isSpawned) NetWalkSpeed = value; else localWalkSpeed = value; }
    }
    public float SprintSpeed
    {
        get => isSpawned ? NetSprintSpeed : localSprintSpeed;
        set { if (isSpawned) NetSprintSpeed = value; else localSprintSpeed = value; }
    }

    bool isSpawned = false;

    PlayerInputHandler handler;

    void OnEnable()
    {
        if(handler == null)
        {
            handler = GetComponent<PlayerInputHandler>();
        }

        localHp = initialHp;
        localStamina = initialStamina;
        localWalkSpeed = initialWalkSpeed;
        localSprintSpeed = initialSprintSpeed;
    }

    public override void Spawned()
    {
        isSpawned = true;

        NetHp = initialHp;
        NetStamina = initialStamina;
        NetWalkSpeed = initialWalkSpeed;
        NetSprintSpeed = initialSprintSpeed;
    }

    public override void FixedUpdateNetwork()
    {
        if (!isSpawned)
        {
            return;
        }

        GetInput(out NetworkInputData input);

        // 스태미나 증감
        if (input.isSprinting && input.moveInput != Vector2.zero)
        {
            ConsumeStamina();
        }
        else
        {
            RegenStamina();
        }
    }

    void FixedUpdate()
    {
        if (isSpawned)
        {
            return;
        }

        NetworkInputData input = handler.GetInputData();
        // 스태미나 증감
        if (input.isSprinting && input.moveInput != Vector2.zero)
        {
            ConsumeStamina();
        }
        else
        {
            RegenStamina();
        }
    }

    #region Stamina Control
    void ConsumeStamina()
    {
        Stamina -= staminaConsumeRate * (isSpawned ? Runner.DeltaTime : Time.deltaTime);
        if(Stamina < 0)
        {
            Stamina = 0;
        }
    }

    void RegenStamina()
    {
        Stamina += staminaRegenRate * (isSpawned ? Runner.DeltaTime : Time.deltaTime);
        if (Stamina > initialStamina)
        {
            Stamina = initialStamina;
        }
    }
    #endregion
}
