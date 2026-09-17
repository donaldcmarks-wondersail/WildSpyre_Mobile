using UnityEngine;

/// <summary>
/// Extended freeze state for abilities or events that stun the enemy.
/// Duration defaults to 1.5s but can be set before entering via board.stunDuration (future extension).
/// </summary>
public class EnemyState_Stunned : IEnemyState
{
    private float _stunTimer;
    private const float DefaultStunDuration = 1.5f;

    public void Enter(EnemyBlackboard board)
    {
        _stunTimer = DefaultStunDuration;
        board.mover?.Stop();
        board.anim?.SetBool("isStunned", true);

        // Same fix as EnemyState_TakeDamage: attackCooldownTimer only ticks down inside
        // EnemyState_Attack.Update(), so being interrupted into Stunned mid-attack would
        // otherwise orphan it and permanently block Chase's re-entry into Attack.
        board.attackCooldownTimer = 0f;
    }

    public void Update(EnemyBlackboard board)
    {
        _stunTimer -= Time.deltaTime;
    }

    public void FixedUpdate(EnemyBlackboard board) { }

    public void Exit(EnemyBlackboard board)
    {
        board.anim?.SetBool("isStunned", false);
    }

    public string CheckTransitions(EnemyBlackboard board)
    {
        if (_stunTimer > 0f) return null;

        if (board.hasTarget) return "Chase";
        return board.HasPatrolRoute ? "Patrol" : "Idle";
    }
}
