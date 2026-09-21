using UnityEngine;

/// <summary>
/// Interrupt hit-stun. Only entered when a hit actually INTERRUPTS the enemy (CTRL_EnemyHealth
/// decides that — a plain hit no longer knocks it out of what it's doing). Cancels the ability
/// in progress and halts movement for a brief reaction window; the separate ability lockout timer
/// (board.abilitiesLockedUntil, set by CTRL_EnemyHealth) keeps it from using abilities afterwards.
/// The "TakeDamage" animator trigger is fired by CTRL_EnemyHealth on every hit, not here.
/// Returns to Chase or Patrol once the reaction window expires.
/// </summary>
public class EnemyState_TakeDamage : IEnemyState
{
    private float _reactionTimer;
    private const float ReactionDuration = 0.35f;

    public void Enter(EnemyBlackboard board)
    {
        _reactionTimer = ReactionDuration;
        board.mover?.Stop();

        // Whatever it was doing is over: stop the ability coroutines and switch off ability
        // hitboxes (body-contact stays on — it's still alive).
        board.abilities?.CancelActiveAbilities(board, includeBodyHitbox: false);

        // attackCooldownTimer only ever ticks down inside EnemyState_Attack.Update() — being
        // yanked in here mid-attack (e.g. a player stomp) would otherwise orphan it at
        // whatever value it had, permanently blocking Chase's re-entry into Attack since
        // nothing else would ever decrement it again. Clear it so the enemy is simply ready
        // to attack again once it recovers.
        board.attackCooldownTimer = 0f;
    }

    public void Update(EnemyBlackboard board)
    {
        _reactionTimer -= Time.deltaTime;
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board) { }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (_reactionTimer > 0f) return null;

        if (board.hasTarget) return "Chase";
        return board.HasPatrolRoute ? "Patrol" : "Idle";
    }
}
