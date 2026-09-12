using UnityEngine;

/// <summary>
/// Enemy executes its preferred ability against the player.
/// Holds position during the attack cooldown, then re-evaluates range.
///
/// Ability alert (SO_EnemyBehavior.useAbilityAlert): each attack begins with a
/// telegraph animation trigger and an abilityAlertDelay wind-up, during which the
/// enemy holds position, before the ability actually fires.
/// </summary>
public class EnemyState_Attack : IEnemyState
{
    private bool _windingUp;
    private float _windUpTimer;

    public void Enter(EnemyBlackboard board)
    {
        board.mover?.Stop();
        BeginAttack(board);
    }

    public void Update(EnemyBlackboard board)
    {
        // Ability wind-up: hold until the telegraph delay elapses, then fire.
        if (_windingUp)
        {
            _windUpTimer -= Time.deltaTime;
            if (_windUpTimer > 0f) return;

            _windingUp = false;
            FireAbility(board);
            return;
        }

        board.attackCooldownTimer -= Time.deltaTime;

        // Off cooldown and still in range — attack again without leaving this
        // state. (CheckTransitions can't signal this itself: returning this
        // state's own name is treated as "no transition" and Enter() wouldn't
        // re-run, so the re-attack has to happen here instead.)
        if (board.attackCooldownTimer <= 0f && board.hasTarget && board.target != null)
        {
            float dist = Vector2.Distance(board.rb.position, board.target.position);
            if (dist <= board.AttackRange)
                BeginAttack(board);
        }
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board)
    {
        _windingUp = false;
    }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (_windingUp) return null;              // committed to the attack
        if (board.attackCooldownTimer > 0f) return null;

        if (!board.hasTarget || board.target == null)
            return "Idle";

        float dist = Vector2.Distance(board.rb.position, board.target.position);
        if (dist > board.AttackRange)
            return "Chase";

        // Still in range — Update() re-attacks; no transition needed.
        return null;
    }

    /// <summary>
    /// Starts one attack. With ability alert enabled, fires the telegraph trigger
    /// and waits abilityAlertDelay before the ability goes off; otherwise the
    /// ability fires immediately.
    /// </summary>
    private void BeginAttack(EnemyBlackboard board)
    {
        SO_EnemyBehavior behavior = board.profile.behavior;

        if (behavior.useAbilityAlert)
        {
            if (!string.IsNullOrEmpty(behavior.abilityAlertTrigger))
                board.anim?.SetTrigger(behavior.abilityAlertTrigger);

            if (behavior.abilityAlertDelay > 0f)
            {
                _windingUp = true;
                _windUpTimer = behavior.abilityAlertDelay;
                return;
            }
        }

        FireAbility(board);
    }

    private void FireAbility(EnemyBlackboard board)
    {
        board.anim?.SetTrigger("Attack");
        board.attackCooldownTimer = board.profile.behavior.attackCooldown;
        board.abilities?.TryUseAbility(board.profile.behavior.preferredAbilityName, board);
    }
}
