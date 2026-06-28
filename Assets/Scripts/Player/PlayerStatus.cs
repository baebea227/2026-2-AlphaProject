using Fusion;
using UnityEngine;

public class PlayerStatus : NetworkBehaviour
{
    [Header("Basic Status")]
    public int initialHp;
    public float initialStamina;
    public float staminaConsumeRate;
    public float staminaRegenRate;

    [Networked] public int Hp { get; set; }
    [Networked] public float Stamina { get; set; }
    [Space(15)]

    [Header("Movement")]
    public float initialWalkSpeed;
    public float initialSprintSpeed;

    [Networked] public float WalkSpeed { get; set; }
    [Networked] public float SprintSpeed { get; set; }

    public override void Spawned()
    {
        Hp = initialHp;
        Stamina = initialStamina;
        WalkSpeed = initialWalkSpeed;
        SprintSpeed = initialSprintSpeed;
    }

    public override void FixedUpdateNetwork()
    {
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

    #region Stamina Control
    void ConsumeStamina()
    {
        Stamina -= staminaConsumeRate * Runner.DeltaTime;
        if(Stamina < 0)
        {
            Stamina = 0;
        }
    }

    void RegenStamina()
    {
        Stamina += staminaRegenRate * Runner.DeltaTime;
        if (Stamina > initialStamina)
        {
            Stamina = initialStamina;
        }
    }
    #endregion
}
